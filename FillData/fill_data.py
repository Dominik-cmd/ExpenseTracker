"""
ExpenseTracker — Bank Statement Data Fill Script
=================================================
Parses 4 OTP banka PDF bank statements and sends each transaction
to the ExpenseTracker webhook API as synthetic SMS messages.

Usage:
    python fill_data.py preview       # Show what will be sent (dry run)
    python fill_data.py send          # Actually send all transactions
    python fill_data.py send --start N  # Resume from transaction #N
"""

import fitz, sys, json, os, re, time
import urllib.request
from datetime import datetime

sys.stdout.reconfigure(encoding="utf-8")

# ──────────────────────────── Configuration ────────────────────────────
FOLDER       = os.path.dirname(os.path.abspath(__file__))
ACCOUNT      = "SI56 0400 1008 6442 606"
WEBHOOK_URL  = "https://expenses.iadore.space/api/webhooks/sms"
WEBHOOK_SECRET = "CDAUaccwn6xsELFVzsI9BGaI4GgcMPT3n_e8ekWhEkM"
DELAY_SECONDS = 0.3  # pause between requests to avoid overwhelming the API

# ──────────────────── PDF Column X-Ranges (pixels) ─────────────────────
COL_DATE_VAL = (40, 100)
COL_TXID     = (100, 150)
COL_PARTY    = (150, 305)
COL_BREME    = (305, 365)
COL_DOBRO    = (365, 420)
COL_STANJE   = (415, 448)
COL_OPIS     = (448, 600)


def in_range(x, col):
    return col[0] <= x < col[1]


def parse_amount(text):
    """Parse Slovenian number format: 1.234,56 → 1234.56"""
    text = text.strip().replace(".", "").replace(",", ".")
    try:
        return float(text)
    except ValueError:
        return None


def extract_party_name(party_text):
    """Extract meaningful name from party column, skipping self/addresses."""
    lines = [l.strip() for l in party_text.split("\n") if l.strip()]
    skip_patterns = [
        r"^04\d{3}-\d+",
        r"^SI56\d+",
        r"BALIGAČ DOMINIK",
        r"ULICA",
        r"^\d{4}\s+(MARIBOR|LJUBLJANA)",
        r"^Ref\.\:",
        r"^namena:",
        r"^\d{13}$",
        r"^04000-",
        r"^OTP banka",
        r"Slovenska cesta",
    ]
    for line in lines:
        if any(re.search(p, line) for p in skip_patterns):
            continue
        if line and len(line) > 1:
            return line
    return ""


def extract_transactions_from_pdf(pdf_path):
    doc = fitz.open(pdf_path)
    transactions = []

    for page_num in range(doc.page_count):
        page = doc[page_num]
        blocks = page.get_text("dict")["blocks"]

        spans = []
        for block in blocks:
            if "lines" in block:
                for line in block["lines"]:
                    for span in line["spans"]:
                        x = span["bbox"][0]
                        y = span["bbox"][1]
                        text = span["text"].strip()
                        if text:
                            spans.append((x, y, text))

        # Row anchors = 10-digit transaction IDs
        row_anchors = sorted(
            y for x, y, text in spans
            if in_range(x, COL_TXID) and re.match(r"^\d{10}$", text)
        )
        if not row_anchors:
            continue

        for i, anchor_y in enumerate(row_anchors):
            y_min = anchor_y - 2
            y_max = (row_anchors[i + 1] - 2) if i + 1 < len(row_anchors) else anchor_y + 36

            row = {"datum_val": [], "txid": [], "party": [], "breme": [], "dobro": [], "stanje": [], "opis": []}

            for x, y, text in spans:
                if y_min <= y < y_max:
                    if   in_range(x, COL_DATE_VAL): row["datum_val"].append((y, text))
                    elif in_range(x, COL_TXID):     row["txid"].append(text)
                    elif in_range(x, COL_PARTY):    row["party"].append((y, text))
                    elif in_range(x, COL_BREME):    row["breme"].append(text)
                    elif in_range(x, COL_DOBRO):    row["dobro"].append(text)
                    elif in_range(x, COL_STANJE):   row["stanje"].append(text)
                    elif in_range(x, COL_OPIS):     row["opis"].append((y, text))

            # ── Dates ──
            date_texts = [
                d[1] for d in sorted(row["datum_val"], key=lambda t: t[0])
                if re.match(r"\d{2}\.\d{2}\.\d{4}", d[1])
            ]
            if not date_texts:
                continue
            datum_val = date_texts[0]

            # ── Transaction ID ──
            txid = row["txid"][0] if row["txid"] else ""

            # ── Amounts ──
            breme_amt = parse_amount(row["breme"][0]) if row["breme"] else None
            dobro_amt = parse_amount(row["dobro"][0]) if row["dobro"] else None

            # ── Description ──
            opis = " ".join(t[1] for t in sorted(row["opis"], key=lambda t: t[0])).strip()

            # ── Party ──
            party_text = "\n".join(t[1] for t in sorted(row["party"], key=lambda t: t[0])).strip()

            # ── Direction ──
            is_credit = dobro_amt is not None and dobro_amt > 0
            is_debit  = breme_amt is not None and breme_amt > 0

            # Handle negative debit (= refund/credit)
            if row["breme"] and not is_debit:
                raw = row["breme"][0].strip()
                if raw.startswith("-"):
                    parsed = parse_amount(raw.lstrip("-"))
                    if parsed:
                        dobro_amt, breme_amt = parsed, None
                        is_credit, is_debit = True, False

            amount = breme_amt if is_debit else dobro_amt
            if not amount:
                continue

            # ── Classify: POS vs Transfer ──
            is_transfer   = False
            recipient_name = ""

            if "Prejemnik:" in party_text:
                is_transfer = True
                m = re.search(r"Prejemnik:\s*(.+?)(?:\s+SI56|\s+\d{13})", party_text, re.DOTALL)
                if m:
                    recipient_name = m.group(1).replace("\n", " ").strip()
                else:
                    m2 = re.search(r"Prejemnik:\s*(.+?)$", party_text, re.MULTILINE)
                    if m2:
                        recipient_name = m2.group(1).strip()
            elif is_credit:
                is_transfer = True
                recipient_name = extract_party_name(party_text)
            elif is_debit:
                name = extract_party_name(party_text)
                if name:
                    recipient_name = name

            transfer_keywords = [
                "PLAČA", "PLAČILO", "TRAJNIK", "POLOG", "DVIG", "MESEČNI STROŠKI",
                "OTP banka", "PRISPEVEK", "RAČ. ŠT.", "IR3", "PRILIV",
            ]
            if any(kw.upper() in opis.upper() for kw in transfer_keywords):
                is_transfer = True

            transactions.append({
                "datum_val": datum_val,
                "txid": txid,
                "amount": amount,
                "is_debit": is_debit,
                "is_credit": is_credit,
                "is_transfer": is_transfer,
                "opis": opis,
                "party_text": party_text,
                "recipient_name": recipient_name,
            })

    doc.close()
    return transactions


def build_sms(tx):
    """Build an SMS matching one of the 3 OTP banka SMS formats."""
    date = tx["datum_val"]
    amount_str = f"{tx['amount']:.2f}".replace(".", ",")

    if tx["is_credit"]:
        payer   = tx["recipient_name"] or tx["opis"] or "UNKNOWN"
        purpose = tx["opis"] or "PRILIV"
        return (
            f"Priliv {date}; Racun: {ACCOUNT}; "
            f"Placnik: {payer}; Namen: {purpose}; "
            f"Znesek: {amount_str} EUR OTP banka d.d."
        )

    if tx["is_transfer"] and tx["is_debit"]:
        recipient = tx["recipient_name"] or tx["opis"] or "UNKNOWN"
        purpose   = tx["opis"] or "PLAČILO"
        return (
            f"Odliv {date}; Iz racuna: {ACCOUNT}; "
            f"Prejemnik: {recipient}; Namen: {purpose}; "
            f"Znesek: {amount_str} EUR OTP banka d.d."
        )

    merchant = tx["opis"] or "UNKNOWN"
    return (
        f"POS NAKUP {date} 12:00, kartica ***0000, "
        f"znesek {amount_str} EUR, {merchant}, MARIBOR SI. "
        f"Info: 0000000000. OTP banka"
    )


def get_sms_type(tx):
    if tx["is_credit"]:
        return "Priliv"
    if tx["is_transfer"]:
        return "Odliv"
    return "POS"


# ══════════════════════════════ Main ══════════════════════════════
def load_all_transactions():
    pdfs = sorted(f for f in os.listdir(FOLDER) if f.endswith(".pdf"))
    all_txns = []
    for pdf_name in pdfs:
        path = os.path.join(FOLDER, pdf_name)
        txns = extract_transactions_from_pdf(path)
        for tx in txns:
            tx["source_pdf"] = pdf_name
        all_txns.extend(txns)
    return all_txns


def preview(transactions):
    """Print a readable table of all transactions that will be sent."""
    current_pdf = None
    for i, tx in enumerate(transactions):
        src = tx["source_pdf"]
        if src != current_pdf:
            current_pdf = src
            print(f"\n{'═' * 105}")
            print(f"  📄 {src}")
            print(f"{'═' * 105}")
            print(
                f"  {'#':>3}  {'Date':<12} {'Type':<7} {'Dir':<7} {'Amount':>10}  "
                f"Description"
            )
            print(f"  {'─'*3}  {'─'*12} {'─'*7} {'─'*7} {'─'*10}  {'─'*55}")

        idx       = i + 1
        date      = tx["datum_val"]
        tx_type   = get_sms_type(tx)
        direction = "CREDIT" if tx["is_credit"] else "DEBIT"
        amount    = tx["amount"]
        desc      = tx["opis"][:55]

        print(f"  {idx:>3}  {date:<12} {tx_type:<7} {direction:<7} {amount:>10.2f}  {desc}")

    # Summary
    total   = len(transactions)
    pos     = sum(1 for t in transactions if get_sms_type(t) == "POS")
    odliv   = sum(1 for t in transactions if get_sms_type(t) == "Odliv")
    priliv  = sum(1 for t in transactions if get_sms_type(t) == "Priliv")
    debit_total  = sum(t["amount"] for t in transactions if t["is_debit"])
    credit_total = sum(t["amount"] for t in transactions if t["is_credit"])

    print(f"\n{'═' * 105}")
    print(f"  SUMMARY")
    print(f"{'═' * 105}")
    print(f"  Total transactions : {total}")
    print(f"  POS purchases      : {pos}")
    print(f"  Odliv (transfers)  : {odliv}")
    print(f"  Priliv (credits)   : {priliv}")
    print(f"  Total debits       : {debit_total:,.2f} EUR")
    print(f"  Total credits      : {credit_total:,.2f} EUR")
    print(f"{'═' * 105}")
    print()
    print("  Run with 'send' to POST all transactions to the webhook.")
    print("  Run with 'send --start N' to resume from transaction #N.")


def send_transactions(transactions, start_from=1):
    """Send each transaction to the webhook API."""
    total = len(transactions)
    success = 0
    failed  = 0
    duplicates = 0

    print(f"\n  Sending {total - start_from + 1} transactions to {WEBHOOK_URL}")
    print(f"  Starting from #{start_from}...\n")

    for i, tx in enumerate(transactions):
        idx = i + 1
        if idx < start_from:
            continue

        sms_text   = build_sms(tx)
        tx_type    = get_sms_type(tx)
        date       = tx["datum_val"]
        # Build a reasonable sentStamp from the value date
        day, month, year = date.split(".")
        sent_stamp = f"{year}-{month}-{day}T12:00:00Z"

        payload = json.dumps({
            "from": "OTP banka",
            "text": sms_text,
            "sentStamp": sent_stamp,
        }).encode("utf-8")

        req = urllib.request.Request(
            WEBHOOK_URL,
            data=payload,
            headers={
                "Content-Type": "application/json",
                "X-Webhook-Secret": WEBHOOK_SECRET,
            },
            method="POST",
        )

        try:
            with urllib.request.urlopen(req, timeout=15) as resp:
                body = json.loads(resp.read().decode("utf-8"))
                status = body.get("status", "unknown")

                if status == "accepted":
                    success += 1
                    icon = "✅"
                elif status == "duplicate":
                    duplicates += 1
                    icon = "⏭️"
                else:
                    icon = "❓"

                desc_short = tx["opis"][:35]
                print(
                    f"  {icon} #{idx:>3}/{total}  {date}  {tx_type:<7} "
                    f"{tx['amount']:>9.2f} EUR  {desc_short:<35}  → {status}"
                )

        except Exception as e:
            failed += 1
            print(f"  ❌ #{idx:>3}/{total}  {date}  ERROR: {e}")

        time.sleep(DELAY_SECONDS)

    print(f"\n{'═' * 80}")
    print(f"  Done!  ✅ {success} accepted  ⏭️ {duplicates} duplicates  ❌ {failed} failed")
    print(f"{'═' * 80}")


if __name__ == "__main__":
    transactions = load_all_transactions()

    if len(sys.argv) < 2 or sys.argv[1] == "preview":
        preview(transactions)
    elif sys.argv[1] == "send":
        start = 1
        if "--start" in sys.argv:
            idx = sys.argv.index("--start")
            start = int(sys.argv[idx + 1])
        send_transactions(transactions, start_from=start)
    else:
        print("Usage: python fill_data.py [preview|send] [--start N]")

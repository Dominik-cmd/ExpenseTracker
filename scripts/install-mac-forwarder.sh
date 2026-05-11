#!/usr/bin/env bash
# install-mac-forwarder.sh
# Installs the SMS forwarder that reads chat.db and POSTs OTP bank messages to your webhook.
# Usage:
#   ./install-mac-forwarder.sh
#   ./install-mac-forwarder.sh --webhook-url "https://..." --webhook-secret "..."

set -euo pipefail

# ── Colours ────────────────────────────────────────────────────────────────────
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
BLUE='\033[0;34m'; BOLD='\033[1m'; RESET='\033[0m'

info()    { echo -e "${BLUE}▸${RESET} $*"; }
success() { echo -e "${GREEN}✓${RESET} $*"; }
warn()    { echo -e "${YELLOW}⚠${RESET} $*"; }
error()   { echo -e "${RED}✗${RESET} $*" >&2; }
header()  { echo -e "\n${BOLD}$*${RESET}"; }

# ── Defaults ───────────────────────────────────────────────────────────────────
WEBHOOK_URL=""
WEBHOOK_SECRET=""
SENDER_PATTERN="OTP%"
POLL_INTERVAL=120
INSTALL_DIR="$HOME/bin"
SOURCE_DIR="$HOME/code/sms-forwarder"
CONFIG_FILE="$HOME/.sms-forwarder.config.json"
STATE_FILE="$HOME/.sms-forwarder-state.json"
PLIST_PATH="$HOME/Library/LaunchAgents/com.expense-tracker.sms-forwarder.plist"
LOG_DIR="$HOME/Library/Logs"
BINARY="$INSTALL_DIR/sms-forwarder"

# ── Argument parsing ───────────────────────────────────────────────────────────
while [[ $# -gt 0 ]]; do
  case $1 in
    --webhook-url)    WEBHOOK_URL="$2";    shift 2 ;;
    --webhook-secret) WEBHOOK_SECRET="$2"; shift 2 ;;
    --sender-pattern) SENDER_PATTERN="$2"; shift 2 ;;
    --poll-interval)  POLL_INTERVAL="$2";  shift 2 ;;
    -h|--help)
      echo "Usage: $0 [--webhook-url URL] [--webhook-secret SECRET]"
      echo "       [--sender-pattern PATTERN] [--poll-interval SECONDS]"
      exit 0 ;;
    *) error "Unknown argument: $1"; exit 1 ;;
  esac
done

# ── Banner ─────────────────────────────────────────────────────────────────────
echo ""
echo -e "${BOLD}╔═══════════════════════════════════════════╗${RESET}"
echo -e "${BOLD}║   Expense Tracker — Mac SMS Forwarder     ║${RESET}"
echo -e "${BOLD}╚═══════════════════════════════════════════╝${RESET}"
echo ""

# ── macOS check ────────────────────────────────────────────────────────────────
if [[ "$(uname)" != "Darwin" ]]; then
  error "This script only runs on macOS."
  exit 1
fi

MACOS_VERSION=$(sw_vers -productVersion)
info "macOS $MACOS_VERSION detected"

# ── Prerequisites ──────────────────────────────────────────────────────────────
header "1. Checking prerequisites"

if ! xcode-select -p &>/dev/null; then
  warn "Xcode Command Line Tools not found. Installing..."
  xcode-select --install
  echo ""
  warn "After the installer finishes, re-run this script."
  exit 0
fi
success "Xcode Command Line Tools found"

if ! command -v swiftc &>/dev/null; then
  error "swiftc not found even though Xcode CLT is installed. Try: sudo xcode-select --reset"
  exit 1
fi
SWIFT_VERSION=$(swiftc --version 2>&1 | head -1)
success "Swift compiler: $SWIFT_VERSION"

# ── Webhook config ─────────────────────────────────────────────────────────────
header "2. Webhook configuration"

if [[ -z "$WEBHOOK_URL" ]]; then
  echo -n "  Webhook URL (e.g. https://expenses.example.com/api/webhooks/sms): "
  read -r WEBHOOK_URL
fi

if [[ -z "$WEBHOOK_URL" ]]; then
  error "Webhook URL is required."
  exit 1
fi

if [[ -z "$WEBHOOK_SECRET" ]]; then
  echo -n "  Webhook secret (X-Webhook-Secret header value): "
  read -rs WEBHOOK_SECRET
  echo ""
fi

if [[ -z "$WEBHOOK_SECRET" ]]; then
  error "Webhook secret is required."
  exit 1
fi

success "Webhook URL: $WEBHOOK_URL"
success "Webhook secret: ${WEBHOOK_SECRET:0:4}****"

# ── Directory setup ────────────────────────────────────────────────────────────
header "3. Creating directories"

mkdir -p "$SOURCE_DIR" "$INSTALL_DIR" "$LOG_DIR"
success "Created $SOURCE_DIR"
success "Created $INSTALL_DIR"

# ── Write Swift source ─────────────────────────────────────────────────────────
header "4. Writing Swift source"

cat > "$SOURCE_DIR/forwarder.swift" << 'SWIFT_EOF'
import Foundation
import SQLite3

// ── Config ─────────────────────────────────────────────────────────────────────
struct Config: Codable {
    let webhookURL: String
    let webhookSecret: String
    let senderPattern: String
    let pollMaxBatchSize: Int
}

// ── State ──────────────────────────────────────────────────────────────────────
struct State: Codable {
    var lastRowId: Int64
}

let configPath = NSString(string: "~/.sms-forwarder.config.json").expandingTildeInPath
let statePath  = NSString(string: "~/.sms-forwarder-state.json").expandingTildeInPath
let chatDbPath = NSString(string: "~/Library/Messages/chat.db").expandingTildeInPath

func loadConfig() -> Config {
    let data = try! Data(contentsOf: URL(fileURLWithPath: configPath))
    return try! JSONDecoder().decode(Config.self, from: data)
}

func loadState() -> State {
    guard let data = try? Data(contentsOf: URL(fileURLWithPath: statePath)),
          let state = try? JSONDecoder().decode(State.self, from: data) else {
        return State(lastRowId: 0)
    }
    return state
}

func saveState(_ state: State) {
    let data = try! JSONEncoder().encode(state)
    try! data.write(to: URL(fileURLWithPath: statePath))
}

// ── attributedBody extraction ──────────────────────────────────────────────────
// iOS 16+ stores message text in a binary NSAttributedString blob.
// We find the UTF-8 text embedded after the "NSString" marker in the typedstream.
func extractTextFromAttributedBody(_ data: Data) -> String? {
    let marker = "NSString".data(using: .utf8)!
    guard let markerRange = data.range(of: marker) else { return nil }

    var offset = markerRange.upperBound

    // Skip typedstream framing: typically 1-3 bytes of length/type info
    // The string content starts with a length prefix; try offsets 1..4
    for skip in 1...4 {
        let start = offset + skip
        guard start < data.count else { continue }
        let remaining = data.subdata(in: start..<data.count)
        if let text = String(data: remaining, encoding: .utf8),
           !text.isEmpty,
           text.unicodeScalars.first?.value ?? 0 > 31 {
            // Trim to printable content (stop at first non-printable run)
            let printable = text.prefix(while: { $0.asciiValue ?? 0 > 0 })
            let result = String(printable).trimmingCharacters(in: .controlCharacters)
            if result.count > 5 { return result }
        }
    }

    // Fallback: scan forward for a run of printable ASCII/UTF-8 bytes
    var start = markerRange.upperBound + 1
    while start < data.count {
        if data[start] > 0x1F && data[start] < 0x80 {
            let end = data[start...].firstIndex(where: { $0 < 0x20 }) ?? data.endIndex
            if end > start + 10,
               let text = String(data: data[start..<end], encoding: .utf8) {
                return text
            }
        }
        start += 1
    }
    return nil
}

// ── Main ───────────────────────────────────────────────────────────────────────
let config = loadConfig()
var state  = loadState()

var db: OpaquePointer?
guard sqlite3_open_v2(chatDbPath, &db, SQLITE_OPEN_READONLY, nil) == SQLITE_OK else {
    fputs("ERROR: Cannot open chat.db — check Full Disk Access permission\n", stderr)
    exit(1)
}
defer { sqlite3_close(db) }

let sql = """
    SELECT
        m.ROWID,
        h.id           AS sender,
        m.date         AS apple_date,
        m.text         AS text_body,
        m.attributedBody AS attr_body
    FROM message m
    JOIN handle h ON m.handle_id = h.rowid
    WHERE h.id LIKE ?
      AND m.ROWID > ?
      AND m.is_from_me = 0
    ORDER BY m.ROWID ASC
    LIMIT ?
    """

var stmt: OpaquePointer?
guard sqlite3_prepare_v2(db, sql, -1, &stmt, nil) == SQLITE_OK else {
    fputs("ERROR: Failed to prepare SQL statement\n", stderr)
    exit(1)
}
defer { sqlite3_finalize(stmt) }

sqlite3_bind_text(stmt, 1, config.senderPattern, -1, unsafeBitCast(-1, to: sqlite3_destructor_type.self))
sqlite3_bind_int64(stmt, 2, state.lastRowId)
sqlite3_bind_int(stmt, 3, Int32(config.pollMaxBatchSize))

let session = URLSession(configuration: .default)
var forwarded = 0
var lastProcessedRowId = state.lastRowId

while sqlite3_step(stmt) == SQLITE_ROW {
    let rowId     = sqlite3_column_int64(stmt, 0)
    let sender    = String(cString: sqlite3_column_text(stmt, 1))
    let appleDate = sqlite3_column_int64(stmt, 2)

    // Convert Apple epoch (nanoseconds since 2001-01-01) to Unix milliseconds
    let unixMs = Int64((Double(appleDate) / 1e9 + 978307200.0) * 1000.0)

    // Get message text: prefer text column, fall back to attributedBody
    var body: String? = nil
    if let rawText = sqlite3_column_text(stmt, 3) {
        body = String(cString: rawText)
    }
    if (body == nil || body!.isEmpty),
       let blobPtr = sqlite3_column_blob(stmt, 4) {
        let blobLen = sqlite3_column_bytes(stmt, 4)
        let blobData = Data(bytes: blobPtr, count: Int(blobLen))
        body = extractTextFromAttributedBody(blobData)
    }

    guard let text = body, !text.isEmpty else {
        print("Row \(rowId): empty body, skipping")
        lastProcessedRowId = rowId
        continue
    }

    // Build payload
    let payload: [String: Any] = [
        "from":      sender,
        "text":      text,
        "sentStamp": String(unixMs)
    ]
    let bodyData = try! JSONSerialization.data(withJSONObject: payload)

    var request = URLRequest(url: URL(string: config.webhookURL)!)
    request.httpMethod = "POST"
    request.httpBody   = bodyData
    request.setValue("application/json",    forHTTPHeaderField: "Content-Type")
    request.setValue(config.webhookSecret,  forHTTPHeaderField: "X-Webhook-Secret")
    request.timeoutInterval = 30

    let semaphore = DispatchSemaphore(value: 0)
    var success = false

    session.dataTask(with: request) { _, response, error in
        if let error = error {
            fputs("ERROR sending row \(rowId): \(error.localizedDescription)\n", stderr)
        } else if let http = response as? HTTPURLResponse {
            if http.statusCode >= 200 && http.statusCode < 300 {
                success = true
                print("Forwarded row \(rowId) from \(sender) (\(text.prefix(60))...)")
            } else {
                fputs("ERROR: webhook returned \(http.statusCode) for row \(rowId)\n", stderr)
            }
        }
        semaphore.signal()
    }.resume()
    semaphore.wait()

    if !success {
        // Stop on failure — retry from here on next run
        break
    }

    lastProcessedRowId = rowId
    forwarded += 1
}

state.lastRowId = lastProcessedRowId
saveState(state)

if forwarded == 0 {
    print("No new messages (lastRowId=\(state.lastRowId))")
} else {
    print("Done — forwarded \(forwarded) message(s), lastRowId=\(state.lastRowId)")
}
SWIFT_EOF

success "Wrote $SOURCE_DIR/forwarder.swift"

# ── Write config ───────────────────────────────────────────────────────────────
header "5. Writing config"

cat > "$CONFIG_FILE" << EOF
{
  "webhookURL": "$WEBHOOK_URL",
  "webhookSecret": "$WEBHOOK_SECRET",
  "senderPattern": "$SENDER_PATTERN",
  "pollMaxBatchSize": 50
}
EOF
chmod 600 "$CONFIG_FILE"
success "Wrote $CONFIG_FILE (mode 600)"

# ── Compile ────────────────────────────────────────────────────────────────────
header "6. Compiling Swift binary"

info "Compiling (this takes ~15 seconds)..."
swiftc -O -o "$BINARY" "$SOURCE_DIR/forwarder.swift"
chmod +x "$BINARY"
success "Compiled to $BINARY"

# ── Unload existing agent if present ──────────────────────────────────────────
if [[ -f "$PLIST_PATH" ]]; then
  warn "Existing launchd agent found — unloading before reinstall"
  launchctl unload "$PLIST_PATH" 2>/dev/null || true
fi

# ── Install launchd agent ──────────────────────────────────────────────────────
header "7. Installing launchd agent"

cat > "$PLIST_PATH" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key>
  <string>com.expense-tracker.sms-forwarder</string>

  <key>ProgramArguments</key>
  <array>
    <string>$BINARY</string>
  </array>

  <key>RunAtLoad</key>
  <true/>

  <key>StartInterval</key>
  <integer>$POLL_INTERVAL</integer>

  <key>StandardOutPath</key>
  <string>$LOG_DIR/sms-forwarder.out.log</string>

  <key>StandardErrorPath</key>
  <string>$LOG_DIR/sms-forwarder.err.log</string>

  <key>EnvironmentVariables</key>
  <dict>
    <key>HOME</key>
    <string>$HOME</string>
  </dict>
</dict>
</plist>
EOF

launchctl load "$PLIST_PATH"
success "Installed and loaded $PLIST_PATH"
success "Agent polls every ${POLL_INTERVAL}s and starts on login"

# ── Full Disk Access ───────────────────────────────────────────────────────────
header "8. Full Disk Access (REQUIRED — manual step)"

echo ""
echo -e "  ${BOLD}The forwarder needs Full Disk Access to read chat.db.${RESET}"
echo -e "  macOS requires you to grant this manually."
echo ""
echo -e "  ${BOLD}Steps:${RESET}"
echo -e "  1. System Settings will open to Privacy & Security → Full Disk Access"
echo -e "  2. Click the ${BOLD}+${RESET} button"
echo -e "  3. Press ${BOLD}⌘ Shift G${RESET} and type: ${BOLD}$INSTALL_DIR${RESET}"
echo -e "  4. Select ${BOLD}sms-forwarder${RESET} and click Open"
echo -e "  5. Toggle it ${BOLD}ON${RESET}"
echo -e "  6. Return here and press ${BOLD}Enter${RESET}"
echo ""

info "Opening System Settings to Full Disk Access..."
open "x-apple.systempreferences:com.apple.preference.security?Privacy_AllFiles"

echo -n "  Press Enter once you've granted Full Disk Access... "
read -r

# ── Verification ───────────────────────────────────────────────────────────────
header "9. Verification"

info "Running forwarder once to verify..."
if "$BINARY" 2>&1; then
  success "Forwarder ran successfully"
else
  warn "Forwarder exited with an error — check the output above"
  warn "If you see 'Cannot open chat.db', Full Disk Access was not granted correctly"
fi

echo ""
info "Checking launchd agent is loaded..."
if launchctl list | grep -q "com.expense-tracker.sms-forwarder"; then
  success "Agent is running"
else
  warn "Agent not found in launchctl list — try: launchctl load $PLIST_PATH"
fi

# ── Summary ────────────────────────────────────────────────────────────────────
header "10. Summary"

echo ""
echo -e "  ${GREEN}${BOLD}Installation complete.${RESET}"
echo ""
echo -e "  ${BOLD}Files installed:${RESET}"
echo -e "  • Binary:    $BINARY"
echo -e "  • Source:    $SOURCE_DIR/forwarder.swift"
echo -e "  • Config:    $CONFIG_FILE"
echo -e "  • State:     $STATE_FILE (auto-created on first run)"
echo -e "  • Agent:     $PLIST_PATH"
echo -e "  • Logs:      $LOG_DIR/sms-forwarder.{out,err}.log"
echo ""
echo -e "  ${BOLD}Useful commands:${RESET}"
echo -e "  Watch logs:    tail -f $LOG_DIR/sms-forwarder.out.log"
echo -e "  Manual run:    $BINARY"
echo -e "  Stop agent:    launchctl unload $PLIST_PATH"
echo -e "  Start agent:   launchctl load   $PLIST_PATH"
echo -e "  Re-forward all: echo '{\"lastRowId\": 0}' > $STATE_FILE && $BINARY"
echo -e "  Update config: nano $CONFIG_FILE && launchctl unload $PLIST_PATH && launchctl load $PLIST_PATH"
echo ""
echo -e "  ${BOLD}Next steps:${RESET}"
echo -e "  1. On your iPhone: Settings → Messages → Text Message Forwarding → enable this Mac"
echo -e "  2. Send a test transaction or trigger an OTP SMS"
echo -e "  3. Check the processing queue in the Expense Tracker dashboard"
echo ""

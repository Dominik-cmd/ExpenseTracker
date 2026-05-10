# Dashboard Redesign — Addendum

This is a follow-up to `expense-tracker-improvements.md`. The first pass made the dashboard analytical when it should be **at-a-glance**. The user wants to use the dashboard for daily awareness (recent activity, where money is going, income vs spending), not for analysis. Analysis lives on the Monthly/Yearly report pages.

This addendum specifies what to change. Apply these changes on top of the existing dashboard implementation.

## Guiding Principle for the Dashboard

The dashboard answers two questions:

1. **"What did I just spend money on?"** → Recent transactions
2. **"Where is my money going lately?"** → Category breakdown + Income vs Spending

That's it. Anything more analytical (what changed vs last month, daily trend with rolling averages, anomaly detection, comparison tables) lives on the report pages. The dashboard should load fast, read in under 5 seconds, and not require thinking.

The Monthly report page is where the user goes when they want to investigate. The dashboard is where they go to check in.

## Layout Change

### Current layout (to remove or move)

- Four-tile / two-tile KPI row → **replace with single horizontal strip**
- "Spending by category" horizontal bar chart → **replace with leaderboard component**
- "What changed" comparison table → **remove from dashboard, keep on Monthly report**
- "Spending trend" chart (rolling averages) → **move to Monthly report**
- "Top merchants" table → **move to Monthly report or render as small footer widget**

### New layout

```
┌──────────────────────────────────────────────────────────────────┐
│  TOP STRIP (one horizontal line, plain text, no cards)          │
│  This month €566  ·  On pace €1,750  ·  Net 30d +€350           │
│  [LLM narrative sentence, italic, one line, max ~15 words]      │
├─────────────────────────┬────────────────────────────────────────┤
│                         │                                        │
│  RECENT TRANSACTIONS    │  SPENDING THIS MONTH                   │
│  (15 rows, clickable)   │  (category leaderboard, top 8 + Other)│
│                         │                                        │
├─────────────────────────┴────────────────────────────────────────┤
│                                                                  │
│  INCOME VS SPENDING (last 6 months)                             │
│  [keep existing bar chart unchanged]                            │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

Three primary widgets only. No exceptions on the dashboard route.

## Detailed Component Specs

### 1. Top Strip (replaces KPI tile row)

Single horizontal line at the top of the dashboard, before any cards. Plain text styling, no card wrapper, no background tint, no borders. Just three numbers separated by a middle-dot character.

**Format:**

```
This month €566  ·  On pace €1,750  ·  Net 30d +€350
```

- "This month" = month-to-date spend (no decimals, no thousands separator under €1,000)
- "On pace" = projected month-end spend, computed as `mtdSpend / daysElapsed * daysInMonth`, rounded to nearest €10
- "Net 30d" = income minus spending over rolling 30 days, with explicit sign (+/-) and color (green positive, red negative)

The labels ("This month", "On pace", "Net 30d") in lowercase muted text. The numbers in larger, foreground-colored text. Reads as a single line at standard font sizes (~16-18px).

**Below the strip:** the LLM narrative sentence, italic, smaller text, one line maximum. If no narrative is available (no LLM provider enabled), render nothing — collapse the row entirely. No placeholder, no skeleton.

Remove the KPI tile components entirely. They are not used elsewhere; delete the components.

### 2. Category Leaderboard (replaces "Spending by category" chart)

Replace the horizontal bar chart with a vertical leaderboard component. The current chart has rendering issues (duplicated labels inside and outside bars, oversaturated colors creating eye strain) and the format is suboptimal for 8+ categories.

**Component structure:**

Each row in the leaderboard:
- 12px circular color dot (from category color)
- Category name (left-aligned, medium weight)
- Amount in € (right-aligned, no decimals if >€100, two decimals if <€100)
- Percentage of total (right-aligned, after amount, muted text)
- Thin horizontal progress bar below (or to the right) showing relative size — width = `amount / topAmount * 100%`

Rows are sorted by amount descending. Show top 8 categories explicitly, collapse remainder into "Other (N categories)" as the 9th row.

**Styling:**
- Dots use the stored category color, but the rest of the row uses neutral colors — no per-row colored backgrounds
- Progress bars use the category color at ~30-40% opacity, on a subtle track background
- Row height around 40-48px, comfortable scanning rhythm
- Hover state: subtle background tint, cursor pointer
- Click action: navigates to `/transactions?categoryId=X&from=<startOfMonth>&to=<today>`

**Header above the list:**
- Title: "Spending this month"
- Subtitle: "Top 8 categories" (muted, smaller)
- Total spend on the right side of the header: "€X,XXX total"

**Empty state:** "No spending recorded yet this month."

This is significantly more readable than a chart for this many categories. Bars are good for 3–5 things; for 8+, a sorted list with inline progress bars is faster to scan.

### 3. Recent Transactions (promote to primary widget)

Promote the existing recent transactions widget to a primary dashboard component. Equal visual weight to the category leaderboard.

**Component structure:**

Each row:
- 8-10px circular color dot from category color
- Date, format `DD MMM` (e.g., `09 May`) — muted, narrow column
- Merchant name, truncate at ~24 characters with ellipsis
- Amount, right-aligned, with sign — debit transactions show `-€15.94` in muted/red, credit transactions show `+€42.29` in green

Show 15 most recent transactions (was previously fewer). Sort by transaction date descending.

**Header:**
- Title: "Recent transactions"
- "See all →" link in the top-right, navigates to `/transactions`

**Row interactions:**
- Hover: subtle background tint
- Click: opens an edit modal/drawer for that transaction (or navigates to a detail view if modal isn't already implemented)

**Empty state:** "No transactions yet. Send a test SMS to verify the webhook."

### 4. Income vs Spending (keep as-is)

Keep the existing 6-month income vs spending bar chart unchanged. It's already working well visually. Place it as the third primary widget below the row containing Recent Transactions and Category Leaderboard.

On wide screens, this can span the full width below the two-column layout. On narrow/mobile screens, all three widgets stack vertically.

### 5. Remove from dashboard, place on Monthly report

The following components currently appear on the dashboard. Move them to the Monthly report page (or remove entirely if duplicated):

- **"What changed" comparison table.** This compares current month to previous month at the category level. The math is currently broken — it compares partial month (9 days into May) to full month (April) and produces -100% / -90% deltas everywhere, which is meaningless. Move to Monthly report **and fix the calculation** to compare same-period-vs-same-period when the current month is partial.

- **"Spending trend" chart with rolling averages.** Analytical, not at-a-glance. Move to Monthly report.

- **"Top merchants" table.** Useful but secondary. Move to Monthly report. If desired, can render as a small footer widget on the dashboard below Income vs Spending — but only as a compact list (top 5, single column), not the full table.

## Backend changes

### Same-period comparison fix

For any current-month vs previous-month comparison (used in the new "What changed" table on the Monthly report page, and in the top-strip "On pace" / projection logic):

When the user is currently viewing the in-progress current month, compare against the **same number of days** of the previous month, not the full previous month.

Example: today is May 9. Comparison should be `May 1–9 this year` vs `April 1–9 this year`, not `May 1–9` vs `full April`.

Implementation:

```csharp
public async Task<MonthComparison> GetCurrentVsPreviousAsync(DateOnly today)
{
    var startOfThisMonth = new DateOnly(today.Year, today.Month, 1);
    var dayOfMonth = today.Day;
    var startOfLastMonth = startOfThisMonth.AddMonths(-1);
    var sameDayLastMonth = startOfLastMonth.AddDays(dayOfMonth - 1);

    var current = await SumByCategoryAsync(startOfThisMonth, today);
    var previous = await SumByCategoryAsync(startOfLastMonth, sameDayLastMonth);

    return BuildComparison(current, previous);
}
```

For the Monthly report page when viewing a *completed* past month, compare against the full previous month as before. Only the "current in-progress month vs previous" view needs the same-period logic.

### Top strip data

Add a single endpoint that returns the top-strip values, since they're shown together:

```
GET /api/analytics/dashboard/strip
→ {
    monthToDate: 565,
    onPace: 1750,
    netLast30: 350,
    netLast30Income: 5666,
    netLast30Spending: 5316
  }
```

Frontend renders these into the strip without any additional computation.

### Existing dashboard endpoint

The existing `GET /api/analytics/dashboard` should now return only what the new dashboard needs:
- `recentTransactions` (last 15)
- `categoryLeaderboard` (top 8 + other, current month only)
- `incomeVsSpending` (last 6 months, unchanged)

Drop the fields that drove the removed widgets (KPI tiles, what-changed comparison, spending trend, top merchants). Those fields move to a new `GET /api/analytics/monthly` payload extension or stay where they are, as appropriate for the Monthly report page.

If the frontend still references removed fields anywhere, clean them up.

## LLM narrative prompt update

Tighten the dashboard narrative prompt to produce shorter, sharper output. The current prompt produces 50+ word, two-sentence outputs that read as academic. The dashboard sentence should be one line, max 15 words.

**Replace the dashboard narrative user prompt template with:**

```
Write ONE sentence (max 15 words) describing how this month's spending is going.

Style requirements:
- Lead with the takeaway, not the math
- Don't start with "Your spending is" or "This month is" — start with the observation
- Reference one specific number or merchant only if it carries the meaning
- Match the tone of these examples:
  - "Tracking 12% above usual; one big insurance payment is the cause."
  - "Quiet month so far — €566 spent, normal daily pace."
  - "On pace for a high month; dining and travel running hot."
  - "Below normal — last month's insurance bill is gone, baseline spending is steady."
  - "Tracking normally."

Input data:
- Today: {today}
- Days into current month: {dayOfMonth} of {daysInMonth}
- Month-to-date spend: €{mtdSpend}
- Same days last month: €{sameDaysLastMonth}
- Projected month-end: €{projectedMonthEnd}
- Last full month total: €{lastMonthTotal}
- 30-day rolling spend: €{rolling30}
- 30-day rolling income: €{rolling30Income}
- Top spending category MTD: {topCategory} (€{topCategoryAmount})
- Largest single transaction last 30d: {largestMerchant} €{largestAmount} ({largestCategory})
- Transactions last 30d: {txnCount}

Output: ONE sentence. No greeting, no padding, no second sentence.
```

The `sameDaysLastMonth` field is new — backend must compute and pass this in. It's the previous-month spending over the same number of days into that month.

If the LLM still produces multiple sentences, the system prompt should reinforce: "Output is one sentence only. Stop after the first period."

## Visual / styling notes

- The dashboard should feel **lighter** than the previous version. Less ink on the page, more whitespace, fewer card borders.
- Top strip is plain text, no card. The transition from header to content should feel seamless.
- The two side-by-side widgets (Recent Transactions, Category Leaderboard) get card treatment — subtle background, rounded corners, comfortable padding.
- Income vs Spending chart sits in a card matching the others.
- Avoid hard-saturated category colors. Reduce saturation of all category colors to ~50-60% of their current values for chart use. The dot indicators and legend can use full saturation, but areas of color (bars, fills) should be muted.
- Color hierarchy: hero numbers (top strip) get foreground emphasis; widget content uses standard text color; muted/secondary info uses ~60% opacity.

## Mobile / responsive

On viewports under ~768px:
- Top strip wraps to two lines if needed (the three numbers stay together if they fit)
- Recent Transactions and Category Leaderboard stack vertically (full-width each)
- Income vs Spending chart stays full-width
- Narrative sentence may wrap to two lines

Test at 375px (small phone) — content should not horizontally scroll.

## Build order for this addendum

1. Build the new top strip component (deterministic, no LLM needed)
2. Build the category leaderboard component (deterministic)
3. Promote recent transactions to primary widget with category color dots
4. Remove "What changed", "Spending trend", "Top merchants" from dashboard
5. Update existing dashboard endpoint to drop unused fields
6. Wire same-period comparison logic for Monthly report
7. Update LLM narrative prompt template
8. Trigger narrative regeneration so existing cached narratives are replaced with the tighter format

## Acceptance criteria

The redesigned dashboard succeeds if:

- A user opens the dashboard and within 5 seconds knows: their spend pace, recent transactions, where money went this month
- No widget on the dashboard requires more than a glance to interpret
- The narrative sentence is one line and immediately useful
- The Monthly report page (separately) holds all the analytical content that was removed

The dashboard is for daily check-ins. The Monthly report is for analysis. Don't blur the line.

# UI-A61 — Lighthouse Secret and Mimic Price (0.3.98)

## User changes

All paid Mimic and Mermimic recruits cost **1 star**. Free naval appearance proxies stay free. Change menu affordability, displayed price, native charge admission, private paid-birth observation and genuine investment consistently. Preserve historical paid investment/refunds; do not reprice existing units. This supersedes the historical three-star requirement.

Ten consecutive genuine clicks on the same visible lighthouse during the player's own active turn grant **1,000 stars**. Clicking elsewhere resets the sequence. Each completed group of ten can award again. Invalid/drag/blocked input and a session, player, turn or map change reset it. Counter progress is private transient UI state; it is neither saved nor broadcast. No ability, hint, popup, sound, particle, camera focus or map animation is added. Normal native click behavior remains.

## Command and persistence

The tenth click submits a complete command through the existing framed native Build carrier and online transport. Add LighthouseSecret=13 without renumbering existing kinds. No unit is needed or allocated. Use the existing actor/coordinate receipt domain (historically named LedgerCityReceipt) to persist exactly-once commitment. There is no schema size change or amount supplied by the client: the only reward is the fixed constant 1000.

Planning verifies supplied started state, session readiness, exact lineage/revision/sequence, actor/turn/alive player, visible native lighthouse position, ledger and native treasury fingerprint, and integer capacity. It never reads the local player's UI or click counter during simulation. An already committed exact intent returns its durable receipt without paying again. Conflicting/stale intents reject. Commitment updates Currency and the ledger atomically with rollback; no unit actions, score, income, population, improvements or fog change.

The silent presentation branch completes the native callback immediately. A committed current-state receipt authorizes ResourceEvents.RefreshWallets(actor), which synchronizes the wallet and star label without native income animation or sound. Never refresh UI from detached command simulation.

## Verification and release

Exercise 9/10/20 clicks, other tile, different lighthouse, drag, UI contact, blocked targeting, suppressed/throwing original callbacks, repeated programmatic selections, stale context and multiplayer observer guards. Exercise detached plan, commit, exact duplicate before/after load/turn changes, conflicts, hidden/non-lighthouse target, arithmetic overflow, rollback and independent peer equality. Retain full model/UI/native metadata/compiler/installer gates; fixture success is not native gameplay acceptance.

Alex handles native playtesting. Install only while closed, own mod only, no new old-mod backup. Keep the existing runtime, launcher and saves. Primary distribution remains the combined Mac installer plus Windows preparation-only ZIP; matching online builds required.

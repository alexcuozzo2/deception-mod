# A39 — Native movement focus and portable build preparation

Alex reports an extra click/jiggle after moving units and explicitly wants all custom post-move focus restoration removed. This supersedes A36's accepted-movement reselection requirement, including ordinary Dash units, Spies, Spymaids and Mimics.

## Selection

Native gameplay owns selection after movement. Remove the accepted-movement callback capture, deferred reselection and their supporting hooks. Never call SelectUnit or write selection merely because movement finished. Keep deliberate user inspection, actual/apparent toggles and read-only observation of native selection. Contextual targets may refresh for a unit the native game actually selected; do not reacquire a cleared selection to show them.

Keep action budgets unchanged: genuine Spies/Spymaids move OR use Subvert/Blackout. Native Dash behavior remains native. Preserve owner/viewer/visibility/turn guards and stale target cancellation.

## Portable implementation

Continue toward one shareable .polymod with Windows support; include Linux where the shared implementation carries over. Alex has someone who can test Windows. This Mac has no Windows game binaries/runtime or Windows compiler, and dependencies must not be downloaded automatically.

Implement buildable shared native helper sources, Windows/Linux/Mac build recipes and standalone native harnesses; make ABI/export/exception rules explicit. Separate a compiled helper's evidence from admission of a native game build. Portable profile descriptors must identify architecture, image path/hash and per-bridge binary/digest/seam resources without borrowing Mac offsets or accepting arbitrary profiles.

Provide a read-only, explicit-path target inventory and a small source handoff for the tester. Do not bundle proprietary game assemblies, Mac loader repairs or personal state. Survey hashes and standalone harnesses cannot prove the Windows game calling convention, exception unwinding, native hook ownership, save/load, gameplay or multiplayer secrecy. Those remain separate acceptance gates before registering a Windows profile. Preserve exact Mac admission until then.

## Verification and installation

Capture current 0.3.78 archive and logs, recognize its exact replacement hash with tests, update Hub metadata, run check/build and install the new mod with the game closed. Alex confirmed the game is closed. Preserve the adapted PolyMod1.3 loader and all other frameworks/configuration. Include the everyday launcher's own read-only check after installation. Alex leads native gameplay tests; do not automatically launch or edit saves.

Record source/model/native-harness/metadata evidence separately from user gameplay acceptance. No claim of Windows or Linux release support until the actual target has been tested.

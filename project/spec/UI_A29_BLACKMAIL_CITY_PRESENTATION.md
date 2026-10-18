# A29 — Blackmail city feedback, diplomacy and Surveillance

This revision supersedes A28's cost and payment timing. It incorporates Alex's subsequent hostility, own-turn income and peace rules.

- Blackmail setup costs **3 stars** on a visible enemy city. Multiple players may independently blackmail a city; one actor cannot duplicate its own mark.
- The victim's income is reduced by 1 star per mark on its own turn. The blackmailer receives 1 star per mark on the **blackmailer's own turn**, like an embassy. No out-of-turn credit; no negative treasury. Payment and collection each run once per turn and survive reload.
- Starting Blackmail is hostile and removes existing embassies through native diplomacy. Establishing an embassy later is compatible; ongoing payments do not repeatedly trigger hostility.
- Accepted peace destroys all Blackmail between the two tribes, in both directions. A peace offer alone does not. New Blackmail cannot be established during peace. Breaking peace cannot revive expired marks.
- Loss of sight does not end Blackmail. City ownership changes, destruction and defeat end affected records; recapture does not revive them.
- Place original document/crown artwork on the city using native city/embassy geometry. Use the blackmailer's color, keep marks clear of real embassies, distinguish multiple marks, and show them to the victim and authorized observers under normal visibility.
- Map city labels and city information show net output: 5 stars with one mark becomes 4. The blackmailer sees its incoming 1 star under the foreign city through the native embassy-income label. The header reflects both incoming and outgoing Blackmail, without any UI query changing money.
- Establishing Blackmail counts as native first contact. The victim receives native meeting rewards only if the tribes have not met. Each newly blackmailed city presents a concise native popup on its owner's turn naming the city and blackmailer and explaining the reduced income. Reload cannot repeat an acknowledged notice.
- Surveillance uses stronger tint (candidate alpha 0.55, previously 0.42) over one continuous coverage surface, preserving soft native fog boundaries without darker overlaps. Preserve normal fog, hidden enemies, tribe colors and the hold toggle.

Schema6 records independent last-debited and last-collected turns. Schema5 migration marks an old simultaneous transfer as already debited and collected for that turn.

Alex performs native playtesting. No Computer Use, automatic launch or save edits. Preserve logs; install only this mod with an exact verified predecessor backup while the game is closed. Automated fixtures and metadata checks do not establish native gameplay success.

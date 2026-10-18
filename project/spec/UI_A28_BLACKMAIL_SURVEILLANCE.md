# A28 — Blackmail, Surveillance and exposed Mimic boats

Alex's Sep19 2026 extension supersedes conflicting historical rules.

- Intelligence retains its technology name. Its existing Knowledge unlock is now displayed as **Surveillance**; the vision overlay behavior is unchanged.
- Intelligence additionally unlocks **Blackmail**. A player with vision of an opponent's city may select that city and pay4stars once to establish Blackmail. It is a city action without a unit action budget. One player cannot establish duplicate Blackmail on the same city, but multiple opponents may independently blackmail it.
- At each city-owner turn, after native income, each active Blackmail transfers1star from the city owner's treasury to its blackmailer. Treasury never becomes negative. When funds are insufficient, resolve records in canonical city X/Y then blackmailer order; an unpaid installment is not debt. Processing is once per turn, persists through save/load and is idempotent across duplicate callbacks.
- Alex explicitly confirmed that Blackmail continues after the blackmailer loses sight of the city and ends when city ownership changes. Destroyed cities and defeated/missing participants cannot keep paying. No refund is implied. Establishment requires current authorized city vision; persistence never reveals fogged cities.
- Use Alex's blackmail_fixed.png and blackmail_team.png as aligned fixed/team layers. Intelligence's Blackmail unlock uses the inspecting player's team color. Each map-city mark uses that Blackmail owner's team color, with multiple distinct marks when several opponents blackmail the city. Marks obey the viewing player's normal fog.
- A true Mimic using a flying disguise retains that disguise's movement and public sprite. Its owner's actual view uses Mimic-boat artwork over water. Shedding over water leaves an exposed Mimic boat and removes borrowed flight; its own ordinary movement/research rules apply, even if that leaves it stranded. Genuine disguised units retain their actual movement and use A27's incompatible-move shedding.

Alex performs native playtesting. Do not launch or drive the game. Build and validate locally, preserve current logs and original user assets, and install only this mod with an exact verified predecessor backup while the game is closed. Source/fixture/native-metadata checks do not establish in-game success.

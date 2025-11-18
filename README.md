Very quick console app I made to see the global leaderboard but only including people that don't play certain mods
(basically I just wanted a way to replicate [this](https://www.reddit.com/r/osugame/comments/1ieag1j/comment/mah3y33/) reddit post to see where I was on the no-dt leaderboard)

probably won't expand on this program much (literally was just made in 30 minutes for the reason above), feel free to use for whatever

## How to use

Clone the repo into your editor of choice:

<code>git clone https://github.com/Aerodite/osu-mod-leaderboard.git</code>
##

Replace lines 18-19 with your [osu! oauth credentials](https://osu.ppy.sh/home/account/edit#oauth)

<code>private const string ClientId = "727727";
private const string ClientSecret = "secretbox";</code>
##

Then change the mod(s) on line 21 to whatever you want to filter out

<code>private static readonly string[] ExcludedMods = ["DT", "NC"];</code>
##

Leave to run for a bit then you should get a leaderboard of players that don't have those ExcludedMods in their top 200 personal best.

<img width="512" height="395" alt="image" src="https://github.com/user-attachments/assets/487b0aa2-9b52-487d-8f47-837257e1e9b1" />

(the request limit is set to a conservative 240 RPM, which would take ~1-3 minutes for the top 1000 users, feel free to change this but please keep in mind the [osu!api terms of use](https://osu.ppy.sh/docs/#terms-of-use) if you do :D

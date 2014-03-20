fotm
====

World of Warcraft arena FotM monitor.

TODO

Frontend:
- Move generation of image links from server viewmodel to js client view objects
- Add NavBar for FotM section to choose fotms by class, not spec
- Show fotm setups for current playing teams if appropriate section is selected
- Visually separate classes and specs in FotM section
- Add armory links to players
- If user clicks on the row/hovers over fotm setup - show all teams with this setup.


Infrastructure:
- ASAP: setup test queue/environment to not break production with every change to Scanner
- Add proper deployment configuration per region and setup queues/websites for US & KR
- Delete expired (1 week?) entries from leaderboard
- Add correct setup per region
- Set smaller expiration date for teams that were seen only once
- Divide ratings update per number of games if diff is more than 1 to avoid showing 30+ rating changes
- Google analytics


Machine learning:
- Introduce some team jumping in test data
- Compute several clusterings and rank them against each other based on following rules: 
  - Lower score for teams with double/triple healers
  - Lower score for teams with class stacking


Backlog:
- Refactor Messaging
- Add AWS SNS/SQS
- Refactor the horrible code made while trying to create minimum viable product

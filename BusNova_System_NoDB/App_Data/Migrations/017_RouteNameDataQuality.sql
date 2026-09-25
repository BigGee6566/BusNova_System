/*
   017_RouteNameDataQuality.sql

   Corrects one route row with malformed reference data, found by the WP-16
   UI/UX audit's Route List review: RouteCode QGE-PE carried RouteName
   'N20' (a road number, not a route name — every other row names the
   route "<Origin> to <Destination>"), and Origin/Destination in shouting
   caps with the town's name misspelled ("QGEBERHA" instead of
   "Gqeberha"). The report rendered it as "N20 / GQEBERHA / PORT ALFRED",
   next to "Alice to East London / Alice / East London" — a data problem,
   not a display problem, and display-side normalisation would have hidden
   it rather than fixed it.

   Idempotent: only rewrites the row if it is still in the bad state, so a
   second run is a no-op rather than a second edit.
*/

UPDATE dbo.Route
SET RouteName = 'Gqeberha to Port Alfred',
    Origin = 'Gqeberha',
    Destination = 'Port Alfred'
WHERE RouteCode = 'QGE-PE'
  AND (RouteName <> 'Gqeberha to Port Alfred'
       OR Origin <> 'Gqeberha'
       OR Destination <> 'Port Alfred');
GO

PRINT 'Migration 017 applied: RouteCode QGE-PE renamed to Gqeberha to Port Alfred.';
GO

/* ===========================================================================
   015_Tracking.sql   -   WP-10, live tracking and ETA

   Adds dbo.TripPing: one row per position report from a bus in motion.

   Run with sqlcmd -I (QUOTED_IDENTIFIER ON), as with the other migrations.

   ---------------------------------------------------------------------
   THIS TABLE GROWS FASTER THAN EVERYTHING ELSE COMBINED
   ---------------------------------------------------------------------
   A bus reporting every 30 seconds produces 120 rows an hour, 1 000 a day
   on a long route, and a ten-bus fleet running twelve hours a day writes
   roughly 150 000 rows a month. Every other table in this schema measures
   its lifetime row count in hundreds.

   Three consequences are designed for rather than discovered:

     1. The table is narrow. No text, no nullable padding - a position, a
        time and a source.
     2. The index is on (AssignmentID, RecordedAt DESC), because every
        query is "the latest few pings for this trip" and none is "all
        pings ever".
     3. There is no cascade delete from RouteAssignment. Deleting a trip
        with 1 000 pings behind it should be a deliberate act, not
        something that happens silently inside another operation.

   Retention is NOT implemented here. A real deployment needs a job that
   deletes pings older than some window, and choosing that window is an
   operational decision - see the note in TrackingService.

   ---------------------------------------------------------------------
   PRECISION
   ---------------------------------------------------------------------
   DECIMAL(9,6) gives about 11 cm at the equator, which is far finer than
   any consumer GPS. FLOAT would be smaller but introduces rounding that
   makes two identical positions compare unequal, and the geography type
   would be the "correct" answer at the cost of a spatial index nobody
   here needs for "where is bus 3 now".
   =========================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.TripPing'))
BEGIN
    CREATE TABLE dbo.TripPing (
        TripPingID   BIGINT IDENTITY(1,1) PRIMARY KEY,

        AssignmentID INT           NOT NULL,

        Latitude     DECIMAL(9,6)  NOT NULL
                     CONSTRAINT CK_TripPing_Latitude  CHECK (Latitude  BETWEEN  -90 AND  90),
        Longitude    DECIMAL(9,6)  NOT NULL
                     CONSTRAINT CK_TripPing_Longitude CHECK (Longitude BETWEEN -180 AND 180),

        /* Null when the device did not report it. Zero and unknown are
           different things - a stationary bus and a bus with no speed
           sensor must not look the same. */
        SpeedKph     DECIMAL(5,1)  NULL
                     CONSTRAINT CK_TripPing_Speed CHECK (SpeedKph IS NULL OR SpeedKph >= 0),

        HeadingDeg   SMALLINT      NULL
                     CONSTRAINT CK_TripPing_Heading CHECK (HeadingDeg IS NULL OR HeadingDeg BETWEEN 0 AND 359),

        /* Where the fix came from, so a simulated ping is never mistaken
           for a real one. This build only produces 'Simulated'. */
        Source       VARCHAR(12)   NOT NULL
                     CONSTRAINT CK_TripPing_Source CHECK (Source IN ('Device','Driver','Simulated')),

        RecordedAt   DATETIME      NOT NULL DEFAULT GETDATE(),

        CONSTRAINT FK_TripPing_Assignment
            FOREIGN KEY (AssignmentID) REFERENCES dbo.RouteAssignment(AssignmentID)
    );
END
GO

/* The only access pattern: the latest pings for one trip. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TripPing_Assignment_Recorded')
    CREATE INDEX IX_TripPing_Assignment_Recorded
        ON dbo.TripPing(AssignmentID, RecordedAt DESC)
        INCLUDE (Latitude, Longitude, SpeedKph, HeadingDeg);
GO

/* --------------------------------------------------------------------------
   Route endpoints, so a distance-to-destination is computable at all.

   Route already carries Origin and Destination as text. Text cannot be
   subtracted from a position, so the coordinates go alongside it. Both
   nullable: most routes have none yet, and a route without coordinates
   must degrade to "no ETA available" rather than to a fabricated one.
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Route') AND name = 'OriginLatitude')
BEGIN
    ALTER TABLE dbo.Route ADD
        OriginLatitude       DECIMAL(9,6) NULL,
        OriginLongitude      DECIMAL(9,6) NULL,
        DestinationLatitude  DECIMAL(9,6) NULL,
        DestinationLongitude DECIMAL(9,6) NULL;
END
GO

/* Seed the coordinates for the Eastern Cape towns already in the seed
   data, so the map has something to show on a fresh install. Only
   touches rows that have no coordinates yet, so it never overwrites
   anything a user has entered. */
UPDATE r SET
    r.OriginLatitude  = c.Lat, r.OriginLongitude = c.Lon
FROM dbo.Route r
INNER JOIN (VALUES
    ('Alice',        -32.788500, 26.836900),
    ('East London',  -33.015300, 27.911600),
    ('Forte Town',   -32.780000, 26.840000),
    ('Gqeberha',     -33.960800, 25.602200),
    ('Mthatha',      -31.589000, 28.784000),
    ('Queenstown',   -31.897800, 26.875600),
    ('GQEBERHA',     -33.960800, 25.602200),
    ('PORT ALFRED',  -33.594400, 26.891100)
) AS c(Town, Lat, Lon) ON c.Town = r.Origin
WHERE r.OriginLatitude IS NULL;
GO

UPDATE r SET
    r.DestinationLatitude = c.Lat, r.DestinationLongitude = c.Lon
FROM dbo.Route r
INNER JOIN (VALUES
    ('Alice',        -32.788500, 26.836900),
    ('East London',  -33.015300, 27.911600),
    ('Forte Town',   -32.780000, 26.840000),
    ('Gqeberha',     -33.960800, 25.602200),
    ('Mthatha',      -31.589000, 28.784000),
    ('Queenstown',   -31.897800, 26.875600),
    ('GQEBERHA',     -33.960800, 25.602200),
    ('PORT ALFRED',  -33.594400, 26.891100)
) AS c(Town, Lat, Lon) ON c.Town = r.Destination
WHERE r.DestinationLatitude IS NULL;
GO

PRINT '015_Tracking.sql applied: TripPing, 4 Route coordinate columns, 1 index.';
GO

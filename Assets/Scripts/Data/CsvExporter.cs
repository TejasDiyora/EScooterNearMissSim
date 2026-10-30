using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace EScooterSim
{
    /// <summary>
    /// Writes simulation data to CSV for analysis outside Unity.
    ///
    ///   events_[run]_[scenario].csv   one row per near-miss or collision
    ///   run_summary.csv               one row per exported run, appended, so runs of
    ///                                 different scenarios can be compared in one table
    ///
    /// Numbers always use a dot as the decimal separator, whatever the computer's
    /// regional settings are.
    /// </summary>
    public static class CsvExporter
    {
        public const string FolderName = "EScooterSimData";
        public const string SummaryFileName = "run_summary.csv";

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public static string OutputFolder
        {
            get { return Path.Combine(Application.persistentDataPath, FolderName); }
        }

        public static string BuildEventsCsv(IList<NearMissEvent> events)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("run_id,scenario_name,time_s,event_type,conflict_type,trigger,");
            sb.Append("scooter_id,other_agent_type,other_agent_id,scooter_speed_mps,other_speed_mps,");
            sb.Append("min_distance_m,min_ttc_s,signal_state,scooter_heading,reaction_time_s,pos_x,pos_z");
            sb.Append('\n');

            for (int i = 0; i < events.Count; i++)
            {
                NearMissEvent e = events[i];
                sb.Append(Text(e.runId)).Append(',');
                sb.Append(Text(e.scenarioName)).Append(',');
                sb.Append(Number(e.time)).Append(',');
                sb.Append(e.eventType.ToString()).Append(',');
                sb.Append(e.conflictType.ToString()).Append(',');
                sb.Append(Text(e.trigger)).Append(',');
                sb.Append(e.scooterId.ToString(Invariant)).Append(',');
                sb.Append(e.otherType.ToString()).Append(',');
                sb.Append(e.otherId.ToString(Invariant)).Append(',');
                sb.Append(Number(e.scooterSpeed)).Append(',');
                sb.Append(Number(e.otherSpeed)).Append(',');
                sb.Append(Number(e.minGap)).Append(',');
                sb.Append(Number(e.minTtc)).Append(',');
                sb.Append(e.signalState.ToString()).Append(',');
                sb.Append(e.scooterHeading.ToString()).Append(',');
                sb.Append(Number(e.reactionTime)).Append(',');
                sb.Append(Number(e.posX)).Append(',');
                sb.Append(Number(e.posZ));
                sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>Writes the event list of one run. Returns the full path of the file.</summary>
        public static string ExportEvents(string runId, ScenarioConfig config, DataRecorder recorder)
        {
            Directory.CreateDirectory(OutputFolder);
            string fileName = "events_" + FileSafe(runId) + "_" + FileSafe(config.scenarioName) + ".csv";
            string path = Path.Combine(OutputFolder, fileName);
            File.WriteAllText(path, BuildEventsCsv(recorder.Events), Utf8NoBom);
            return path;
        }

        public static string BuildSummaryHeader()
        {
            return "run_id,scenario_name,duration_s,random_seed,"
                + "scooter_count,scooter_speed_mps,reaction_time_s,scooter_brake_mps2,"
                + "vehicle_speed_mps,vehicle_spawn_interval_s,vehicle_passing_clearance_m,right_turn_share,driver_yield_rate,"
                + "pedestrian_count,pedestrian_speed_mps,pedestrian_compliance_rate,"
                + "near_miss_distance_m,ttc_threshold_s,"
                + "near_misses,collisions,"
                + "close_pass_near_misses,pedestrian_near_misses,turn_near_misses,"
                + "close_pass_collisions,pedestrian_collisions,turn_collisions,"
                + "min_near_miss_distance_m,min_near_miss_ttc_s,avg_scooter_speed_at_events_mps,"
                + "encounters,scooter_distance_km,events_per_km";
        }

        public static string BuildSummaryRow(string runId, ScenarioConfig config, DataRecorder recorder)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Text(runId)).Append(',');
            sb.Append(Text(config.scenarioName)).Append(',');
            sb.Append(Number(recorder.Duration)).Append(',');
            sb.Append(config.randomSeed.ToString(Invariant)).Append(',');

            sb.Append(config.scooterCount.ToString(Invariant)).Append(',');
            sb.Append(Number(config.scooterSpeed)).Append(',');
            sb.Append(Number(config.reactionTime)).Append(',');
            sb.Append(Number(config.scooterBrakeDeceleration)).Append(',');

            sb.Append(Number(config.vehicleSpeed)).Append(',');
            sb.Append(Number(config.vehicleSpawnInterval)).Append(',');
            sb.Append(Number(config.vehiclePassingClearance)).Append(',');
            sb.Append(Number(config.rightTurnShare)).Append(',');
            sb.Append(Number(config.driverYieldRate)).Append(',');

            sb.Append(config.pedestrianCount.ToString(Invariant)).Append(',');
            sb.Append(Number(config.pedestrianSpeed)).Append(',');
            sb.Append(Number(config.pedestrianComplianceRate)).Append(',');

            sb.Append(Number(config.nearMissDistance)).Append(',');
            sb.Append(Number(config.ttcThreshold)).Append(',');

            sb.Append(recorder.NearMissCount.ToString(Invariant)).Append(',');
            sb.Append(recorder.CollisionCount.ToString(Invariant)).Append(',');
            sb.Append(recorder.NearMisses(ConflictType.ClosePass).ToString(Invariant)).Append(',');
            sb.Append(recorder.NearMisses(ConflictType.PedestrianCrossing).ToString(Invariant)).Append(',');
            sb.Append(recorder.NearMisses(ConflictType.TurnConflict).ToString(Invariant)).Append(',');
            sb.Append(recorder.Collisions(ConflictType.ClosePass).ToString(Invariant)).Append(',');
            sb.Append(recorder.Collisions(ConflictType.PedestrianCrossing).ToString(Invariant)).Append(',');
            sb.Append(recorder.Collisions(ConflictType.TurnConflict).ToString(Invariant)).Append(',');

            sb.Append(Number(recorder.MinGap)).Append(',');
            sb.Append(Number(recorder.MinTtc)).Append(',');
            sb.Append(Number(recorder.AverageScooterSpeedAtEvents)).Append(',');
            sb.Append(recorder.EncounterCount.ToString(Invariant)).Append(',');
            sb.Append(Number(recorder.ScooterKilometres)).Append(',');
            sb.Append(Number(recorder.EventsPerKilometre));
            return sb.ToString();
        }

        /// <summary>Adds one line for this run to run_summary.csv. Returns the full path of the file.</summary>
        public static string AppendRunSummary(string runId, ScenarioConfig config, DataRecorder recorder)
        {
            Directory.CreateDirectory(OutputFolder);
            string path = Path.Combine(OutputFolder, SummaryFileName);
            StringBuilder sb = new StringBuilder();
            if (!File.Exists(path))
            {
                sb.Append(BuildSummaryHeader()).Append('\n');
            }
            sb.Append(BuildSummaryRow(runId, config, recorder)).Append('\n');
            File.AppendAllText(path, sb.ToString(), Utf8NoBom);
            return path;
        }

        /// <summary>Formats a number for CSV. Infinity and NaN become an empty cell.</summary>
        private static string Number(float value)
        {
            if (float.IsInfinity(value) || float.IsNaN(value)) return "";
            return value.ToString("0.###", Invariant);
        }

        /// <summary>Quotes text when it contains characters that would break a CSV row.</summary>
        private static string Text(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            bool needsQuotes = value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0
                || value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0;
            if (!needsQuotes) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string FileSafe(string value)
        {
            if (string.IsNullOrEmpty(value)) return "run";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            }
            return sb.ToString();
        }
    }
}

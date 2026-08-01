using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Miku.ShaderConverter.Editor
{
    [Serializable]
    public sealed class MikuRebakeRequest
    {
        public string bundlePath = "";
        public string sourceMapPath = "";
        public string planHash = "";
        public string sourceMapHash = "";
        public List<string> jobIds = new List<string>();
    }

    [Serializable]
    public sealed class MikuRebakeTicket
    {
        public string id = Guid.NewGuid().ToString("N");
        public string status = "queued";
        public List<string> diagnostics = new List<string>();
    }

    /// <summary>
    /// Queues explicit Blender-side rebakes. Inspector changes only mark jobs
    /// dirty; this coordinator never starts a process implicitly.
    /// </summary>
    public static class MikuRebakeCoordinator
    {
        static readonly HashSet<string> ActiveJobs = new HashSet<string>(StringComparer.Ordinal);

        public static MikuRebakeTicket Enqueue(MikuRebakeRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var ticket = new MikuRebakeTicket();
            if (string.IsNullOrWhiteSpace(request.bundlePath) || !File.Exists(request.bundlePath))
            {
                ticket.status = "failed";
                ticket.diagnostics.Add("MIKU_BUNDLE_MISSING");
                return ticket;
            }
            if (string.IsNullOrWhiteSpace(request.sourceMapPath) || !File.Exists(request.sourceMapPath))
            {
                ticket.status = "failed";
                ticket.diagnostics.Add("MIKU_SOURCE_MAP_MISSING_FULL_RECONVERT_REQUIRED");
                return ticket;
            }
            JObject sourceMap = JObject.Parse(File.ReadAllText(request.sourceMapPath));
            var actualSourceMapHash = sourceMap["sourceMapHash"]?.Value<string>() ?? "";
            if (!string.IsNullOrEmpty(request.sourceMapHash) && !string.Equals(actualSourceMapHash, request.sourceMapHash, StringComparison.Ordinal))
            {
                ticket.status = "failed";
                ticket.diagnostics.Add("MIKU_SOURCE_MAP_DRIFT_FULL_RECONVERT_REQUIRED");
                return ticket;
            }
            foreach (var jobId in request.jobIds ?? new List<string>())
            {
                if (!ActiveJobs.Add(jobId))
                {
                    ticket.status = "failed";
                    ticket.diagnostics.Add("MIKU_REBAKE_JOB_LOCKED:" + jobId);
                    return ticket;
                }
            }
            ticket.status = "queued";
            return ticket;
        }
    }
}

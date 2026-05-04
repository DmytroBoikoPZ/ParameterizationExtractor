using Quipu.ParameterizationExtractor.Logic.Interfaces;
using Quipu.ParameterizationExtractor.Logic.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Quipu.ParameterizationExtractor.Logic.Configs
{
    public class SourceForScript : ISourceForScript, IAmDSLFriendly
    {
        public SourceForScript()
        {
            RootRecords = new List<RecordsToExtract>();
            TablesToProcess = new List<TableToExtract>();
        }

        [XmlAttribute()]
        public int Order { get; set; }

        [XmlAttribute()]
        public string ScriptName { get; set; }

        /// <summary>
        /// Optional free-form seed query the operator typed in the desktop's Seed tab
        /// (M4 mockup shows a full SELECT). UI-only persistence in v1: the engine still
        /// drives the FK walk from <see cref="RootRecords"/>; this field round-trips through
        /// the workspace JSON so the operator's typed query survives close/reopen.
        /// Null when the workspace was authored from XML / DSL (back-compat).
        /// </summary>
        [XmlIgnore]
        [JsonPropertyName("query"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Query { get; set; }

        public List<RecordsToExtract> RootRecords { get; set; }
        [XmlIgnore]
        IList<RecordsToExtract> ISourceForScript.RootRecords
        {
            get
            {
                return RootRecords;
            }
        }

        public List<TableToExtract> TablesToProcess { get; set; }
        [XmlIgnore]
        IList<TableToExtract> ISourceForScript.TablesToProcess
        {
            get
            {
                return TablesToProcess;
            }
        }

        [XmlAttribute()]
        public string Comments
        {
            get; set;
        }

        public string AsString()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"for script \"{ScriptName}\" take");

            foreach (var r in RootRecords)
                builder.AppendLine(r.AsString());

            builder.AppendLine("consider");

            foreach (var t in TablesToProcess)
                builder.AppendLine(t.AsString());

            return builder.ToString();
        }
    }
}

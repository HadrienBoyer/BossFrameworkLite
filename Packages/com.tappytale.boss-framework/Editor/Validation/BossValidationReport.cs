using System;
using System.Collections.Generic;

namespace TappyTale.BossFight.Editor.Validation
{
    public enum BossValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public readonly struct BossValidationIssue
    {
        public BossValidationIssue(
            BossValidationSeverity severity,
            string message,
            string assetPath,
            string category = "General",
            Action fixAction = null,
            string fixLabel = null)
        {
            Severity = severity;
            Message = message;
            AssetPath = assetPath ?? string.Empty;
            Category = string.IsNullOrWhiteSpace(category) ? "General" : category;
            FixAction = fixAction;
            FixLabel = string.IsNullOrWhiteSpace(fixLabel) ? "Auto-Fix" : fixLabel;
        }

        public BossValidationSeverity Severity { get; }
        public string Message { get; }
        public string AssetPath { get; }
        public string Category { get; }
        public Action FixAction { get; }
        public string FixLabel { get; }
        public bool CanAutoFix => FixAction != null;
    }

    public sealed class BossValidationReport
    {
        private readonly List<BossValidationIssue> _issues = new();

        public IReadOnlyList<BossValidationIssue> Issues => _issues;
        public bool HasErrors { get; private set; }
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public int InfoCount { get; private set; }

        public void Add(
            BossValidationSeverity severity,
            string message,
            string assetPath = "",
            string category = "General",
            Action fixAction = null,
            string fixLabel = null)
        {
            _issues.Add(new BossValidationIssue(severity, message, assetPath, category, fixAction, fixLabel));
            switch (severity)
            {
                case BossValidationSeverity.Error:
                    HasErrors = true;
                    ErrorCount++;
                    break;
                case BossValidationSeverity.Warning:
                    WarningCount++;
                    break;
                default:
                    InfoCount++;
                    break;
            }
        }
    }
}

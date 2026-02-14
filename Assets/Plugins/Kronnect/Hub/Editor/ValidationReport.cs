using System;
using System.Collections.Generic;
using System.Linq;

namespace Kronnect.Hub {

    internal sealed class ValidationReport {
        public readonly List<ValidationEntry> Entries = new List<ValidationEntry>();
        public bool HasEntries => Entries.Count > 0;
        public bool HasInvalid => Entries.Any(e => !e.IsOptionalAction && !e.IsValid);
        public bool HasInvalidWithFix => Entries.Any(e => !e.IsOptionalAction && !e.IsValid && e.FixAction != null);
        public IReadOnlyList<ValidationEntry> SortedEntries => Entries.OrderBy(e => e.IsOptionalAction ? 1 : 0).ToList();

        public void Add(string description, bool isValid, string details, Action fixAction = null, UnityEngine.Object showTarget = null, Action showAction = null, string customFixLabel = "Fix", Func<bool> isStillInvalid = null) {
            Entries.Add(new ValidationEntry {
                Description = description,
                IsValid = isValid,
                Details = details,
                FixAction = fixAction,
                IsStillInvalid = isStillInvalid,
                ShowTarget = showTarget,
                ShowAction = showAction,
                CustomFixLabel = customFixLabel
            });
        }

        public void AddOptionalAction(string description, string details, Action executeAction, UnityEngine.Object showTarget = null, Action showAction = null, string customFixLabel = "Execute") {
            Entries.Add(new ValidationEntry {
                Description = description,
                Details = details,
                IsValid = true,
                FixAction = executeAction,
                IsOptionalAction = true,
                ShowTarget = showTarget,
                ShowAction = showAction,
                CustomFixLabel = customFixLabel
            });
        }

        public void AddWarning(string description, string details, Action showAction = null) {
            Entries.Add(new ValidationEntry {
                Description = description,
                Details = details,
                IsValid = false,
                IsWarning = true,
                FixAction = null,
                ShowAction = showAction
            });
        }

        public void Clear() {
            Entries.Clear();
        }
    }

}

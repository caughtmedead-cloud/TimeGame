using System;
using UnityEngine;

namespace Kronnect.Hub {

    internal sealed class ValidationEntry {
        public string Description;
        public string Details;
        public bool IsValid;
        public bool IsWarning;
        public Action FixAction;
        public Func<bool> IsStillInvalid;
        public UnityEngine.Object ShowTarget;
        public Action ShowAction;
        public bool IsOptionalAction;
        public string CustomFixLabel;

        public bool CanShow => ShowTarget != null || ShowAction != null;
        
        public bool NeedsFix {
            get {
                if (IsOptionalAction) return FixAction != null;
                return !IsValid && FixAction != null && (IsStillInvalid == null || IsStillInvalid());
            }
        }
    }

}


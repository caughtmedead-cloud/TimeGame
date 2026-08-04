namespace Kronnect.Hub {

    internal interface IValidationProvider {
        void Validate(ValidationContext context);
        void OnValidationComplete(ValidationContext context);
    }

}


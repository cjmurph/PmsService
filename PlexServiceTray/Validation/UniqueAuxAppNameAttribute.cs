using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace PlexServiceTray.Validation
{
    class UniqueAuxAppNameAttribute:ValidationAttribute
    {
        private new const string ErrorMessage = "There's already an Auxilliary Application called {0}.";

        private SettingsWindowViewModel _context;

        public override string FormatErrorMessage(string name)
        {
            return string.Format(ErrorMessage, name);
        }

        public override bool IsValid(object value)
        {
            return IsValid(value, null) == ValidationResult.Success;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (validationContext != null)
                _context = validationContext.ObjectInstance as SettingsWindowViewModel;
            var name = value as string;
            base.ErrorMessage = FormatErrorMessage(name);
            if (string.IsNullOrEmpty(name) || _context?.AuxiliaryApplications == null) {
                return ValidationResult.Success;
            }

            return _context.AuxiliaryApplications.Count(a => a.Name == name) > 1 ? new ValidationResult(base.ErrorMessage) : ValidationResult.Success;
        }
    }
}

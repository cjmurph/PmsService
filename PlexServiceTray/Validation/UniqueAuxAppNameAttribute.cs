using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace PlexServiceTray.Validation
{
    class UniqueAuxAppNameAttribute:ValidationAttribute
    {
        private const string _errorMessage = "There's already an Auxilliary Application called {0}.";

        private SettingsWindowViewModel _context;

        public UniqueAuxAppNameAttribute()
        {
        }

        public override string FormatErrorMessage(string name)
        {
            return string.Format(_errorMessage, name);
        }

        public override bool IsValid(object value)
        {
            return IsValid(value, null) == ValidationResult.Success;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (validationContext != null)
                _context = validationContext.ObjectInstance as SettingsWindowViewModel;
            string name = value as string;
            ErrorMessage = FormatErrorMessage(name);
            if (!string.IsNullOrEmpty(name) && _context != null && _context.AuxiliaryApplications != null)
            {
                if (_context.AuxiliaryApplications.Count(a => a.Name == name) > 1)
                    return new ValidationResult(ErrorMessage);
            }
            return ValidationResult.Success;
        }
    }
}

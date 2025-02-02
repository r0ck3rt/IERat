﻿using IERatServer.Properties;
using System;
using System.Globalization;

namespace NClap.Metadata
{
    /// <summary>
    /// Attribute that indicates the associated integer argument member must
    /// be less than a given value.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class MustBeLessThanAttribute : IntegerComparisonValidationAttribute
    {
        /// <summary>
        /// Primary constructor.
        /// </summary>
        /// <param name="target">Value to compare against.</param>
        public MustBeLessThanAttribute(object target) : base(target)
        {
        }

        /// <summary>
        /// Validate the provided value in accordance with the attribute's
        /// policy.
        /// </summary>
        /// <param name="context">Context for validation.</param>
        /// <param name="value">The value to validate.</param>
        /// <param name="reason">On failure, receives a user-readable string
        /// message explaining why the value is not valid.</param>
        /// <returns>True if the value passes validation; false otherwise.
        /// </returns>
        public override bool TryValidate(ArgumentValidationContext context, object value, out string reason)
        {
            if (GetArgumentType(value).IsLessThan(value, Target))
            {
                reason = null;
                return true;
            }

            reason = string.Format(CultureInfo.CurrentCulture, Strings.ValueIsNotLessThan, Target);
            return false;
        }
    }
}

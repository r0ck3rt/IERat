﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using IERatServer.Properties;
using NClap.Exceptions;
using NClap.Metadata;
using NClap.Utilities;

namespace NClap.Parser
{
    /// <summary>
    /// Factory class useful for defining an argument set from reflecting on
    /// attributes associated with a type and its members.
    /// </summary>
    public static class AttributeBasedArgumentDefinitionFactory
    {
        /// <summary>
        /// Defines an argument set based on the reflection-based attributes
        /// stored on the provided type.
        /// </summary>
        /// <param name="typeToReflectOn">The type to inspect.</param>
        /// <param name="attribute">Attribute info for the argument set; if this
        /// argument is present and if there's already an attribute associated
        /// with the type being reflected on, then this one will replace the
        /// other one.</param>
        /// <param name="defaultValues">Optionally provides an object containing
        /// default values not otherwise captured by reflection info.</param>
        /// <param name="fixedDestination">Optionally provides a fixed object
        /// to store values to; regardless of the target object provided,
        /// parsed values will always be stored to this one.</param>
        /// <param name="serviceConfigurer">Optionally provides a service
        /// configurer.</param>
        /// <returns>Created argument set.</returns>
        [CLSCompliant(false)]
        public static ArgumentSetDefinition CreateArgumentSet(
            Type typeToReflectOn,
            ArgumentSetAttribute attribute = null,
            object defaultValues = null,
            object fixedDestination = null,
            ServiceConfigurer serviceConfigurer = null)
        {
            // Find high-level metadata for the argument set.
            var argSetAttrib = attribute ?? GetSetAttributeOrDefault(typeToReflectOn);

            // Construct an empty definition.
            var argSet = new ArgumentSetDefinition(argSetAttrib);

            // Add arguments.
            AddToArgumentSet(
                argSet,
                typeToReflectOn,
                defaultValues: defaultValues,
                fixedDestination: fixedDestination,
                serviceConfigurer: serviceConfigurer);

            return argSet;
        }

        /// <summary>
        /// Adds arguments to an existing argument set based on the reflection-
        /// based attributes stored on the provided type.
        /// </summary>
        /// <param name="argSet">Argument set to add to.</param>
        /// <param name="typeToReflectOn">The type to inspect.</param>
        /// <param name="defaultValues">Optionally provides an object containing
        /// default values not otherwise captured by reflection info.</param>
        /// <param name="fixedDestination">Optionally provides a fixed object
        /// to store values to; regardless of the target object provided,
        /// parsed values will always be stored to this one.</param>
        /// <param name="containingArgument">Optionally provides a reference
        /// to the definition of the argument that "contains" these arguments.
        /// </param>
        /// <param name="serviceConfigurer">Optionally provides a service
        /// configurer.</param>
        [CLSCompliant(false)]
        public static void AddToArgumentSet(
            ArgumentSetDefinition argSet,
            Type typeToReflectOn,
            object defaultValues = null,
            object fixedDestination = null,
            ArgumentDefinition containingArgument = null,
            ServiceConfigurer serviceConfigurer = null)
        {
            // Extract argument descriptors from the defining type.
            var args = GetArgumentDescriptors(
                typeToReflectOn,
                argSet,
                defaultValues,
                fixedDestination,
                containingArgument,
                serviceConfigurer).ToList();

            // Define the arguments.
            argSet.Add(args);

            // If the provided type we're reflecting on has an ArgumentSetAttribute,
            // then add that as auxiliary information.
            var auxiliaryAttrib = TryGetSetAttribute(typeToReflectOn);
            if (auxiliaryAttrib != null)
            {
                argSet.AddAuxiliaryAttribute(auxiliaryAttrib);
            }

            // If the argument set doesn't already have a default assembly associated
            // with it, then fill that out.
            if (argSet.DefaultAssembly == null)
            {
                argSet.DefaultAssembly = typeToReflectOn.GetTypeInfo().Assembly;
            }
        }

        private static ArgumentSetAttribute GetSetAttributeOrDefault(Type typeToReflectOn) =>
            TryGetSetAttribute(typeToReflectOn) ?? new ArgumentSetAttribute();

        private static ArgumentSetAttribute TryGetSetAttribute(Type typeToReflectOn) =>
            typeToReflectOn.GetTypeInfo().GetSingleAttribute<ArgumentSetAttribute>();

        private static IEnumerable<ArgumentDefinition> GetArgumentDescriptors(Type type, ArgumentSetDefinition argSet, object defaultValues, object fixedDestination, ArgumentDefinition containingArgument, ServiceConfigurer serviceConfigurer)
        {
            // Find all fields and properties that have argument attributes on
            // them. For each that we find, capture information about them.
            var argList = GetAllFieldsAndProperties(type, includeNonPublicMembers: true)
                .SelectMany(member => CreateArgumentDescriptorsIfApplicable(member, defaultValues, argSet, fixedDestination, containingArgument, serviceConfigurer));

            // If the argument set attribute indicates that we should also
            // include un-attributed, public, writable members as named
            // arguments, then look for them now.
            if (argSet.Attribute.PublicMembersAreNamedArguments)
            {
                argList = argList.Concat(GetAllFieldsAndProperties(type, includeNonPublicMembers: false)
                    .Where(member => member.IsWritable)
                    .Where(member => member.MemberInfo.GetSingleAttribute<ArgumentBaseAttribute>() == null)
                    .Select(member => CreateArgumentDescriptor(
                        member,
                        new NamedArgumentAttribute(),
                        defaultValues,
                        argSet,
                        fixedDestination,
                        containingArgument,
                        serviceConfigurer)));
            }

            return argList;
        }

        private static IEnumerable<ArgumentDefinition> CreateArgumentDescriptorsIfApplicable(IMutableMemberInfo member, object defaultValues,
            ArgumentSetDefinition argSet, object fixedDestination, ArgumentDefinition containingArgument, ServiceConfigurer serviceConfigurer)
        {
            var descriptors = Enumerable.Empty<ArgumentDefinition>();

            var argAttrib = member.MemberInfo.GetSingleAttribute<ArgumentBaseAttribute>();
            if (argAttrib != null)
            {
                descriptors = descriptors.Concat(
                    new[]
                    {
                        CreateArgumentDescriptor(
                            member,
                            argAttrib,
                            defaultValues,
                            argSet,
                            fixedDestination,
                            containingArgument,
                            serviceConfigurer)
                    });
            }

            return descriptors;
        }

        private static ArgumentDefinition CreateArgumentDescriptor(
            IMutableMemberInfo member,
            ArgumentBaseAttribute attribute,
            object defaultValues,
            ArgumentSetDefinition argSet,
            object fixedDestination,
            ArgumentDefinition containingArgument,
            ServiceConfigurer serviceConfigurer)
        {
            if (!member.IsReadable || !member.IsWritable)
            {
                var declaringType = member.MemberInfo.DeclaringType;

                throw new InvalidArgumentSetException(member, string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.MemberNotSupported,
                    member.MemberInfo.Name,
                    declaringType?.Name));
            }

            var defaultFieldValue = (defaultValues != null) ? member.GetValue(defaultValues) : null;
            return new ArgumentDefinition(member,
                attribute,
                argSet,
                defaultValue: defaultFieldValue,
                fixedDestination: fixedDestination,
                containingArgument: containingArgument,
                serviceConfigurer: serviceConfigurer);
        }

        private static IEnumerable<IMutableMemberInfo> GetAllFieldsAndProperties(Type type, bool includeNonPublicMembers)
        {
            // Generate a list of the fields and properties declared on
            // 'argumentSpecification', and on all types in its inheritance
            // hierarchy.
            var members = new List<IMutableMemberInfo>();
            for (var currentType = type; currentType != null; currentType = currentType.GetTypeInfo().BaseType)
            {
                var bindingFlags =
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.DeclaredOnly;

                if (includeNonPublicMembers)
                {
                    bindingFlags |= BindingFlags.NonPublic;
                }

                members.AddRange(currentType.GetFieldsAndProperties(bindingFlags));
            }

            return members;
        }
    }
}

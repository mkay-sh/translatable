﻿using Optional;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Translatable.Export.Shared.Po;

namespace Translatable.Export.Shared
{
    public enum ResultCode
    {
        NoTranslatablesFound,
        NoTranslationsFound,
        Error
    }

    public class Exporter
    {
        public Option<Catalog, ResultCode> DoExport<TAssemblyLoader>(IEnumerable<string> assemblyFiles) where TAssemblyLoader : IAssemblyLoader, new()
        {
            using (var assemblyLoader = new TAssemblyLoader())
            {
                try
                {
                    assemblyLoader.LoadAssemblies(assemblyFiles);

                    var allTypes = assemblyLoader.Assemblies
                        .SelectMany(s => s.GetTypes());

                    var translatables = allTypes
                        .Where(TypeHelper.IsTranslatable)
                        .ToList();

                    var enums = allTypes
                        .Where(TypeHelper.HasTranslatableAttribute)
                        .ToList();

                    if (!translatables.Any() && !enums.Any())
                        return Option.None<Catalog, ResultCode>(ResultCode.NoTranslationsFound);

                    var catalog = new Catalog();

                    foreach (var translatable in translatables)
                        ExportClass(translatable, catalog);

                    foreach (var type in enums)
                    {
                        ExportEnum(type, catalog);
                    }

                    if (!catalog.Entries.Any())
                        return Option.None<Catalog, ResultCode>(ResultCode.NoTranslationsFound);

                    return catalog.Some<Catalog, ResultCode>();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    Console.WriteLine("Error: " + ex);
                    Console.WriteLine("");
                    Console.WriteLine("LoaderExceptions:");

                    foreach (var loaderException in ex.LoaderExceptions)
                    {
                        Console.WriteLine(loaderException);
                    }

                    return Option.None<Catalog, ResultCode>(ResultCode.Error);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex);
                    return Option.None<Catalog, ResultCode>(ResultCode.Error);
                }
            }
        }

        public void ExportClass(Type translatable, Catalog catalog)
        {
            var properties = translatable.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            var obj = Activator.CreateInstance(translatable);

            foreach (var property in properties)
            {
                if (TypeHelper.IsType(property.PropertyType, typeof(IPluralBuilder)))
                    continue;

                // TODO: allow to access private setters of inherited fields
                if (!property.CanRead)
                    throw new InvalidOperationException($"The property {property.Name} in type {translatable.FullName} is not readable!");

                if (!property.CanWrite)
                    throw new InvalidOperationException($"The property {property.Name} in type {translatable.FullName} is not writable!");

                var comment = TypeHelper.GetTranslatorCommentAttributeValue(property);
                var context = EscapeString(TypeHelper.GetContextAttributeValue(property));

                if (property.PropertyType == typeof(string))
                {
                    var escapedMessage = EscapeString(property.GetValue(obj, null).ToString());

                    catalog.AddEntry(escapedMessage, comment, translatable.FullName, context);

                    continue;
                }

                if (property.PropertyType == typeof(string[]))
                {
                    var value = (string[])property.GetValue(obj, null);

                    if (value.Length != 2)
                        throw new InvalidDataException($"The plural string for {property.Name} must contain two strings: a singular and a plural form. It contained {value.Length} strings.");

                    var escapedSingular = EscapeString(value[0]);
                    var escapedPlural = EscapeString(value[1]);

                    catalog.AddPluralEntry(escapedSingular, escapedPlural, comment, translatable.FullName);

                    continue;
                }

                if (property.PropertyType.IsArray && property.PropertyType.GetElementType().Name == typeof(EnumTranslation<>).Name)
                {
                    // translatable enums will be exported by inspecting all enums with the TranslatableAttribute
                    continue;
                }

                throw new InvalidOperationException($"The type {property.PropertyType} in {translatable.FullName}.{property.Name} is not supported in ITranslatables.");
            }
        }

        public void ExportEnum(Type type, Catalog catalog)
        {
            if (!TypeHelper.HasTranslatableAttribute(type))
                throw new InvalidOperationException("The type is no translatable enum! Add the Attribute Translatable to the enum declaration.");

            foreach (var value in Enum.GetValues(type))
            {
                try
                {
                    var msgid = TypeHelper.GetTranslationAttributeValue(value);
                    var escapedMessage = EscapeString(msgid);
                    var comment = TypeHelper.GetEnumTranslatorCommentAttributeValue(value);
                    var context = EscapeString(TypeHelper.GetEnumContextAttributeValue(value));

                    catalog.AddEntry(escapedMessage, comment, type.FullName, context);
                }
                catch (ArgumentException)
                {
                    throw new InvalidOperationException($"The value {value} in enum {type.Name} does not have the [Translation] attribute. This is required to make it translatable.");
                }
            }
        }

        private string EscapeString(string str)
        {
            return str
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\t", "\\t")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
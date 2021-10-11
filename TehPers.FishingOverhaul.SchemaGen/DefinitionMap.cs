﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Namotion.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TehPers.FishingOverhaul.SchemaGen
{
    public class DefinitionMap
    {
        private static readonly Dictionary<Type, JObject> predefinedSchemas = new()
        {
            [typeof(string)] = new() { ["type"] = "string" },
            [typeof(byte)] = new()
            {
                ["type"] = "integer",
                ["minimum"] = byte.MinValue,
                ["maximum"] = byte.MaxValue,
            },
            [typeof(sbyte)] = new()
            {
                ["type"] = "integer",
                ["minimum"] = sbyte.MinValue,
                ["maximum"] = sbyte.MaxValue,
            },
            [typeof(ushort)] = new()
            {
                ["type"] = "integer",
                ["minimum"] = ushort.MinValue,
                ["maximum"] = ushort.MaxValue,
            },
            [typeof(short)] = new()
            {
                ["type"] = "integer",
                ["minimum"] = short.MinValue,
                ["maximum"] = short.MaxValue,
            },
            [typeof(uint)] = new()
            {
                ["type"] = "integer",
                ["minimum"] = uint.MinValue,
                ["maximum"] = uint.MaxValue,
            },
            [typeof(int)] = new()
            {
                ["type"] = "integer",
                ["minimum"] = int.MinValue,
                ["maximum"] = int.MaxValue,
            },
            [typeof(ulong)] = new()
            {
                ["type"] = "integer",
                ["minimum"] = ulong.MinValue,
                ["maximum"] = ulong.MaxValue,
            },
            [typeof(long)] = new()
            {
                ["type"] = "integer",
                ["minimum"] = long.MinValue,
                ["maximum"] = long.MaxValue,
            },
            [typeof(float)] = new() { ["type"] = "number" },
            [typeof(double)] = new() { ["type"] = "number" },
            [typeof(decimal)] = new() { ["type"] = "number" },
            [typeof(DateTime)] = new()
            {
                ["type"] = "string",
                ["format"] = "date-time",
            },
            [typeof(DateTimeOffset)] = new()
            {
                ["type"] = "string",
                ["format"] = "date-time",
            },
            [typeof(byte[])] = new() { ["type"] = "string" },
            [typeof(Type)] = new() { ["type"] = "string" },
            [typeof(Guid)] = new()
            {
                ["type"] = "string",
                ["format"] = "uuid",
            },
        };

        private static readonly HashSet<Type> arrayTypes = new()
        {
            typeof(IEnumerable<>),
            typeof(IReadOnlyList<>),
            typeof(IList<>),
            typeof(List<>),
            typeof(IImmutableList<>),
            typeof(ImmutableArray<>),
            typeof(ImmutableList<>),
        };

        private static readonly HashSet<Type> dictionaryTypes = new()
        {
            typeof(IReadOnlyDictionary<,>),
            typeof(IDictionary<,>),
            typeof(Dictionary<,>),
            typeof(IImmutableDictionary<,>),
            typeof(ImmutableDictionary<,>),
        };

        public Dictionary<string, JObject> Definitions { get; } = new();

        private string GetDefinitionName(ContextualType refType)
        {
            return refType.Type.FullName
                ?? throw new ArgumentException(
                    "Type not supported for definitions",
                    nameof(refType)
                );
        }

        public JObject Register(ContextualType contextualType)
        {
            var schemaChoices = this.CreateSchemas(contextualType).ToList();
            if (schemaChoices.Count == 1 && !schemaChoices[0].ContainsKey("$ref"))
            {
                // Single choice - just return the definition itself
                return schemaChoices[0];
            }

            // Create union definition: { "oneOf": [...] }
            var oneOf = new JArray();
            foreach (var schemaChoice in schemaChoices)
            {
                oneOf.Add(schemaChoice);
            }

            return new()
            {
                ["oneOf"] = oneOf,
            };
        }

        private IEnumerable<JObject> CreateSchemas(ContextualType contextualType)
        {
            // Nullable types
            if (contextualType.Nullability is not Nullability.NotNullable)
            {
                yield return new() { ["type"] = "null" };
            }

            // Get inner type
            var innerType = contextualType.Type;

            // Predefined schemas
            if (predefinedSchemas.TryGetValue(innerType, out var predefinedDef))
            {
                // Predefined type
                yield return (JObject)predefinedDef.DeepClone();
                yield break;
            }

            // Arrays
            if (innerType.IsArray)
            {
                // Array
                if (innerType.GetArrayRank() != 1)
                {
                    throw new InvalidOperationException(
                        "Schema generation doesn't work with multidimensional arrays."
                    );
                }

                yield return new()
                {
                    ["type"] = "array",
                    ["items"] = this.Register(contextualType.ElementType!),
                };
                yield break;
            }

            // Special generic types
            if (innerType.IsGenericType)
            {
                // Get generic type definition (like List<>)
                var genericDef = innerType.GetGenericTypeDefinition();

                // Array types
                if (DefinitionMap.arrayTypes.Contains(genericDef))
                {
                    yield return new()
                    {
                        ["type"] = "array",
                        ["items"] = this.Register(contextualType.GenericArguments[0]),
                    };
                    yield break;
                }

                // Dictionary types
                if (DefinitionMap.dictionaryTypes.Contains(genericDef))
                {
                    // TODO: support keys that can be converted to strings
                    if (!this.Stringish(contextualType.GenericArguments[0]))
                    {
                        throw new InvalidOperationException(
                            "Schema generation doesn't work with dictionaries with non-string keys."
                        );
                    }

                    yield return new()
                    {
                        ["type"] = "object",
                        ["additionalProperties"] =
                            this.Register(contextualType.GenericArguments[1]),
                    };
                    yield break;
                }
            }

            // Custom types
            yield return this.CreateRef(contextualType);
        }

        private bool Stringish(ContextualType contextualType)
        {
            // Check type
            var innerType = contextualType.Type;
            if (innerType == typeof(string))
            {
                return true;
            }

            // Check type converters
            return TypeDescriptor.GetConverter(innerType) is { } converter
                && converter.CanConvertTo(typeof(string))
                && converter.CanConvertFrom(typeof(string));
        }

        private JObject CreateRef(ContextualType contextualType)
        {
            var definitionName = this.GetDefinitionName(contextualType);
            if (this.Definitions.ContainsKey(definitionName))
            {
                return this.CreateRefObject(definitionName);
            }

            // Set the definition for now to handle recursion
            this.Definitions[definitionName] = new();
            this.Definitions[definitionName] = this.CreateCustomTypeSchema(contextualType);
            return this.CreateRefObject(definitionName);
        }

        private JObject CreateRefObject(string definitionName)
        {
            return new() { ["$ref"] = $"#/definitions/{definitionName}" };
        }

        private JObject CreateCustomTypeSchema(ContextualType contextualType)
        {
            var type = contextualType.Type;
            var typeDescription = type.GetCustomAttributes<DescriptionAttribute>(false)
                .Select(attr => attr.Description)
                .FirstOrDefault();

            return type switch
            {
                { IsClass: true } or { IsValueType: true } => GenerateForClassOrStruct(),
                { IsEnum: true } => GenerateForEnum(),
                _ => throw new InvalidOperationException(),
            };

            JObject GenerateForEnum()
            {
                // Basic information
                var result = new JObject { ["type"] = "string" };
                if (typeDescription is not null)
                {
                    result["description"] = typeDescription;
                }

                // Enum variants
                var variants = new JArray();
                result["enum"] = variants;
                foreach (var variantName in type.GetEnumNames())
                {
                    variants.Add(variantName);
                }

                return result;
            }

            JObject GenerateForClassOrStruct()
            {
                // Basic information
                var result = new JObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                };
                if (typeDescription is not null)
                {
                    result["description"] = typeDescription;
                }

                var required = new JArray();
                var properties = new JObject();
                
                // Properties
                var propertyMembers = type
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Concat(
                        type.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance)
                            .Where(
                                prop => prop.GetCustomAttributes<JsonPropertyAttribute>(true).Any()
                            )
                    );
                var fieldMembers = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Concat(
                        type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                            .Where(
                                prop => prop.GetCustomAttributes<JsonPropertyAttribute>(true).Any()
                            )
                    );
                var members = propertyMembers
                    .Select(prop => new MemberData(prop, prop.ToContextualProperty().PropertyType))
                    .Concat(
                        fieldMembers.Select(
                            field => new MemberData(field, field.ToContextualField().FieldType)
                        )
                    )
                    .Where(
                        memberData => !memberData.Info.GetCustomAttributes<JsonIgnoreAttribute>()
                            .Any()
                    );
                foreach (var memberData in members)
                {
                    // Get property name
                    var name = memberData.Info.GetCustomAttributes<JsonPropertyAttribute>()
                            .Select(attr => attr.PropertyName)
                            .FirstOrDefault()
                        ?? memberData.Info.Name;

                    // Mark property as required if needed
                    var isRequired = memberData.Info
                        .GetCustomAttributes<JsonRequiredAttribute>(true)
                        .Any();
                    if (isRequired)
                    {
                        required.Add(name);
                    }

                    // Create property schema
                    var propSchema = this.CreatePropertySchema(memberData);
                    properties[name] = propSchema;
                }

                if (required.Count > 0)
                {
                    result["required"] = required;
                }

                if (properties.Count > 0)
                {
                    result["properties"] = properties;
                }

                return result;
            }
        }

        private JObject CreatePropertySchema(MemberData memberData)
        {
            var (info, contextualType) = memberData;

            // Create raw schema
            var schema = this.Register(contextualType);

            // Add description
            var description = info.GetCustomAttributes<DescriptionAttribute>(true)
                .Select(attr => attr.Description)
                .FirstOrDefault();
            if (description is not null)
            {
                schema["description"] = description;
            }

            return schema;
        }
    }
}
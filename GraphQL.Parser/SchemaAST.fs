﻿//MIT License
//
//Copyright (c) 2016 Robert Peele
//
//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:
//
//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.
//
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

namespace GraphQL.Parser.SchemaAST
open GraphQL.Parser
open System.Collections.Generic

// This file implements an enhanced form of the AST with information added
// by validating it both for internal consistency and consistency with a schema.
// Generic types in this module with a `'s` generic parameter contain arbitrary information
// supplied by the schema implementation. For example, when the schema implementation is asked
// to resolve a type name, it can provide an `'s` with the resulting type information,
// which will be included in the AST for later use.

/// A primitive value, which needs no extra information.
type Primitive =
    | IntPrimitive of int64
    | FloatPrimitive of double
    | StringPrimitive of string
    | BooleanPrimitive of bool
    | NullPrimitive // although it can't be written as a literal, it does exist

type PrimitiveType =
    | IntType
    | FloatType
    | StringType
    | BooleanType

type ListWithSource<'a> = IReadOnlyList<'a WithSource>

/// Represents type information provided by the schema implementation for validation
/// and for inclusion in the validated AST.
type ISchemaQueryType<'s> =
    abstract member TypeName : string
    abstract member Description : string option
    abstract member Info : 's
    /// Get the fields of this type, keyed by name.
    /// May be empty, for example if the type is a primitive.
    abstract member Fields : IReadOnlyDictionary<string, ISchemaField<'s>>
    /// Get the enumerated values of this type, if it is an enum type.
    abstract member EnumValues : IReadOnlyDictionary<string, ISchemaEnumValue<'s>>
/// Represents a named core type, e.g. a "Time" type represented by an ISO-formatted string.
/// The type may define validation rules that run on values after they have been checked to
/// match the given core type.
and ISchemaVariableType =
    abstract member TypeName : string
    abstract member CoreType : CoreVariableType
    /// Produce an error message if the value is not valid for this type.
    abstract member ValidateValue : Value<'s> -> string option
/// Represents the type of a field, which may be either another queryable type, or
/// a non-queryable value.
and SchemaFieldType<'s> =
    | QueryField of ISchemaQueryType<'s>
    | ValueField of VariableType
/// Represents field information provided by the schema implementation for validation
/// and for inclusion in the validated AST.
and ISchemaField<'s> =
    abstract member DeclaringType : ISchemaQueryType<'s>
    abstract member FieldType : SchemaFieldType<'s>
    abstract member FieldName : string
    abstract member Description : string option
    abstract member Info : 's
    /// Get the possible arguments of this field, keyed by name.
    /// May be empty if the field accepts no arguments.
    abstract member Arguments : IReadOnlyDictionary<string, ISchemaArgument<'s>>
/// Represents argument information provided by the schema implementation for validation
/// and for inclusion in the validated AST.
and ISchemaArgument<'s> =
    abstract member ArgumentName : string
    abstract member ArgumentType : CoreVariableType
    abstract member Description : string option
    abstract member Info : 's
and ISchemaArgumentValue<'s> =
    abstract member Argument : ISchemaArgument<'s>
    abstract member Info : 's
    abstract member Value : ValueExpression<'s>
and ISchemaEnumValue<'s> =
    abstract member DeclaringType : ISchemaQueryType<'s>
    abstract member EnumValueName : string
    abstract member Description : string option
    abstract member Info : 's
and ISchemaDirective<'s> =
    abstract member DirectiveName : string
    abstract member Description : string option
    abstract member Info : 's
    /// Get the possible arguments of this directive, keyed by name.
    /// May be empty if the directive accepts no arguments.
    abstract member Arguments : IReadOnlyDictionary<string, ISchemaArgument<'s>>
and ISchema<'s> =
    /// Return the core type, if any, with the given name.
    /// A core type is a type whose values can be expressed as a `Value` within
    /// a GraphQL document. This encompasses the values that can be provided as
    /// arguments to a field or directive or declared as variables for an operation.
    abstract member ResolveVariableTypeByName : string -> ISchemaVariableType option
    /// Return the type, if any, with the given name. These are types that
    /// may appear in a query and 
    abstract member ResolveQueryTypeByName : string -> ISchemaQueryType<'s> option
    /// Return all types that contain the given enum value name.
    abstract member ResolveEnumValueByName : string -> ISchemaEnumValue<'s> option
    /// Return the directive, if any, with the given name.
    abstract member ResolveDirectiveByName : string -> ISchemaDirective<'s> option
    /// The top-level type that queries select from.
    /// Most likely this will correspond to your DB context type.
    abstract member RootType : ISchemaQueryType<'s>
/// A value within the GraphQL document. This is fully resolved, not a variable reference.
and Value<'s> =
    | PrimitiveValue of Primitive
    | EnumValue of ISchemaEnumValue<'s>
    | ListValue of Value<'s> ListWithSource
    | ObjectValue of IReadOnlyDictionary<string, Value<'s> WithSource>
/// A value expression within the GraphQL document.
/// This may contain references to variables, whose values are not yet
/// supplied.
and ValueExpression<'s> =
    | VariableExpression of VariableDefinition<'s>
    | PrimitiveExpression of Primitive
    | EnumExpression of ISchemaEnumValue<'s>
    | ListExpression of ValueExpression<'s> ListWithSource
    | ObjectExpression of IReadOnlyDictionary<string, ValueExpression<'s> WithSource>
/// Represents a non-nullable value type.
and CoreVariableType =
    | PrimitiveType of PrimitiveType
    | ListType of VariableType
    /// Not possible to declare this type in a GraphQL document, but it exists nonetheless.
    | ObjectType of IReadOnlyDictionary<string, VariableType>
    | NamedType of ISchemaVariableType
and VariableType =
    {
        Type : CoreVariableType
        Nullable : bool
    }
and VariableDefinition<'s> =
    {
        VariableName : string
        VariableType : VariableType
        DefaultValue : Value<'s> option
    }

type Directive<'s> =
    {
        SchemaDirective : ISchemaDirective<'s>
        Arguments : ISchemaArgumentValue<'s> ListWithSource
    }

type Field<'s> =
    {
        SchemaField : ISchemaField<'s>
        Alias : string option
        Arguments : ISchemaArgumentValue<'s> ListWithSource
        Directives : Directive<'s> ListWithSource
        Selections : Selection<'s> ListWithSource
    }
and Selection<'s> =
    | FieldSelection of Field<'s>
    | FragmentSpreadSelection of FragmentSpread<'s>
    | InlineFragmentSelection of InlineFragment<'s>
and FragmentSpread<'s> =
    {
        Fragment : Fragment<'s>
        Directives : Directive<'s> ListWithSource
    }
and Fragment<'s> =
    {
        FragmentName : string
        TypeCondition : ISchemaQueryType<'s>
        Directives : Directive<'s> ListWithSource
        Selections : Selection<'s> ListWithSource
    }
and InlineFragment<'s> =
    {
        TypeCondition : ISchemaQueryType<'s> option
        Directives : Directive<'s> ListWithSource
        Selections : Selection<'s> ListWithSource
    }

type OperationType =
    | Query
    | Mutation

type LonghandOperation<'s> =
    {
        OperationType : OperationType
        OperationName : string option
        VariableDefinitions : VariableDefinition<'s> ListWithSource
        Directives : Directive<'s> ListWithSource
        Selections : Selection<'s> ListWithSource
    }

type Operation<'s> =
    | ShorthandOperation of Selection<'s> ListWithSource
    | LonghandOperation of LonghandOperation<'s>

// Note: we don't include fragment definitions in the schema-validated AST.
// This is because the Fragment<'s> type only makes sense where a fragment is
// used via the spread operator in an operation. It's impossible to resolve variable
// types against the schema at the point where a fragment is defined, because they could
// be different depending on where it's used.
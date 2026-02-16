using System.Text.Json.Serialization;

namespace Ghost.Sdk.Spider.Adapters.GraphQL.Schema;

/// <summary>
/// Represents a GraphQL schema.
/// </summary>
/// <remarks>
/// The schema describes the types, queries, mutations, and subscriptions available
/// in a GraphQL API. This information is typically obtained through introspection.
/// </remarks>
public class GraphQLSchema
{
    /// <summary>
    /// Gets or sets the query type for this schema.
    /// </summary>
    /// <value>The root query type, or null if queries are not supported.</value>
    [JsonPropertyName("queryType")]
    public GraphQLType? QueryType { get; set; }

    /// <summary>
    /// Gets or sets the mutation type for this schema.
    /// </summary>
    /// <value>The root mutation type, or null if mutations are not supported.</value>
    [JsonPropertyName("mutationType")]
    public GraphQLType? MutationType { get; set; }

    /// <summary>
    /// Gets or sets the subscription type for this schema.
    /// </summary>
    /// <value>The root subscription type, or null if subscriptions are not supported.</value>
    [JsonPropertyName("subscriptionType")]
    public GraphQLType? SubscriptionType { get; set; }

    /// <summary>
    /// Gets or sets all types defined in the schema.
    /// </summary>
    /// <value>A list of all types including queries, mutations, objects, interfaces, unions, enums, and scalars.</value>
    [JsonPropertyName("types")]
    public List<GraphQLType> Types { get; set; } = [];

    /// <summary>
    /// Gets or sets the directives supported by this schema.
    /// </summary>
    /// <value>A list of directive definitions available in the schema.</value>
    [JsonPropertyName("directives")]
    public List<GraphQLDirective> Directives { get; set; } = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQLSchema"/> class.
    /// </summary>
    public GraphQLSchema()
    {
    }

    /// <summary>
    /// Finds a type by name in the schema.
    /// </summary>
    /// <param name="typeName">The name of the type to find.</param>
    /// <returns>The type if found; otherwise, null.</returns>
    public GraphQLType? FindType(string typeName)
    {
        return Types.FirstOrDefault(t => t.Name == typeName);
    }

    /// <summary>
    /// Gets all query fields available in the schema.
    /// </summary>
    /// <returns>A list of query fields, or an empty list if no queries are defined.</returns>
    public List<GraphQLField> GetQueryFields()
    {
        return QueryType?.Fields ?? new List<GraphQLField>();
    }

    /// <summary>
    /// Gets all mutation fields available in the schema.
    /// </summary>
    /// <returns>A list of mutation fields, or an empty list if no mutations are defined.</returns>
    public List<GraphQLField> GetMutationFields()
    {
        return MutationType?.Fields ?? new List<GraphQLField>();
    }

    /// <summary>
    /// Gets all subscription fields available in the schema.
    /// </summary>
    /// <returns>A list of subscription fields, or an empty list if no subscriptions are defined.</returns>
    public List<GraphQLField> GetSubscriptionFields()
    {
        return SubscriptionType?.Fields ?? new List<GraphQLField>();
    }

    /// <summary>
    /// Creates a standard introspection query.
    /// </summary>
    /// <returns>A GraphQL introspection query string.</returns>
    public static string CreateIntrospectionQuery()
    {
        return @"
query IntrospectionQuery {
  __schema {
    queryType { name }
    mutationType { name }
    subscriptionType { name }
    types {
      ...FullType
    }
    directives {
      name
      description
      locations
      args {
        ...InputValue
      }
    }
  }
}

fragment FullType on __Type {
  kind
  name
  description
  fields(includeDeprecated: true) {
    name
    description
    args {
      ...InputValue
    }
    type {
      ...TypeRef
    }
    isDeprecated
    deprecationReason
  }
  inputFields {
    ...InputValue
  }
  interfaces {
    ...TypeRef
  }
  enumValues(includeDeprecated: true) {
    name
    description
    isDeprecated
    deprecationReason
  }
  possibleTypes {
    ...TypeRef
  }
}

fragment InputValue on __InputValue {
  name
  description
  type { ...TypeRef }
  defaultValue
}

fragment TypeRef on __Type {
  kind
  name
  ofType {
    kind
    name
    ofType {
      kind
      name
      ofType {
        kind
        name
        ofType {
          kind
          name
          ofType {
            kind
            name
            ofType {
              kind
              name
              ofType {
                kind
                name
              }
            }
          }
        }
      }
    }
  }
}";
    }
}

/// <summary>
/// Represents a GraphQL directive.
/// </summary>
/// <remarks>
/// Directives are used to modify the execution of a query or to provide additional
/// metadata. Common directives include @include, @skip, and @deprecated.
/// </remarks>
public class GraphQLDirective
{
    /// <summary>
    /// Gets or sets the name of the directive.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the description of the directive.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the locations where this directive can be used.
    /// </summary>
    [JsonPropertyName("locations")]
    public List<string> Locations { get; set; } = [];

    /// <summary>
    /// Gets or sets the arguments accepted by this directive.
    /// </summary>
    [JsonPropertyName("args")]
    public List<GraphQLField> Args { get; set; } = [];
}

﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGeneratorCommon;

namespace Unity.Entities.SourceGen.LambdaJobs
{
    // Describes the field in the LambdaJob struct that holds the accessor type for accessing data from entities
    // (ComponentDataFromEntity, BufferFromEntity)
    // These get added as members into the LambdaJob struct.
    class DataFromEntityFieldDescription
    {
        public bool IsReadOnly { get; }
        public ITypeSymbol Type { get; }
        public LambdaJobsPatchableMethod.AccessorDataType AccessorDataType { get; }
        public string FieldName { get; }

        public DataFromEntityFieldDescription(bool isReadOnly, ITypeSymbol type, LambdaJobsPatchableMethod.AccessorDataType accessorDataType)
        {
            IsReadOnly = isReadOnly;
            Type = type;
            AccessorDataType = accessorDataType;
            FieldName = $"__{Type.ToValidVariableName()}_FromEntity";;
        }

        public string JobStructAssign()
        {
            return AccessorDataType switch
            {
                LambdaJobsPatchableMethod.AccessorDataType.ComponentDataFromEntity =>
                    $@"{FieldName} = GetComponentDataFromEntity<{Type.ToFullName()}>({(IsReadOnly ? "true" : "")})",
                LambdaJobsPatchableMethod.AccessorDataType.BufferFromEntity =>
                    $@"{FieldName} = GetBufferFromEntity<{Type.ToFullName()}>({(IsReadOnly ? "true" : "")})",
                _ => ""
            };
        }
    }

    // Rewrite the original lambda body with a few changes:
    // 1. If we are accessing any methods/fields of our declaring type, make sure it is explicit
    //   ("this.EntityMananger.DoThing()" instead of "EntityMananger.DoThing()")
    // 2. Replace all member access with "this" identifiers to "__this" (to access through stored field on job struct)
    // 3. Patch all access through data access through entity methods (GetComponent, SetComponent, etc)
    // 4. Adds trivia for line numbers from original syntax statements
    sealed class LambdaBodyRewriter
    {
        Dictionary<ITypeSymbol, DataFromEntityFieldDescription> DataFromEntityFields { get; }
            = new Dictionary<ITypeSymbol, DataFromEntityFieldDescription>();

        internal static (SyntaxNode rewrittenLambdaExpression, List<DataFromEntityFieldDescription> additionalFields, List<MethodDeclarationSyntax> methodsForLocalFunctions)
            Rewrite(LambdaJobDescription description)
        {
            SemanticModel model = description.SemanticModel;
            SyntaxNode originalLambdaExpression = description.OriginalLambdaExpression;
            List<LambdaCapturedVariableDescription> variablesCapturedOnlyByLocals = description.VariablesCapturedOnlyByLocals;

            var lambdaBodyRewriter = new LambdaBodyRewriter();

            // Find all locations where we are accessing a member on the declaring SystemBase
            // and change them to access through "__this" instead.
            // This also annotates the changed nodes so that we can find them later for patching (and get their original symbols).
            var rewrittenLambdaBodyData = LambdaBodySyntaxReplacer.Rewrite(model, originalLambdaExpression);
            var rewrittenLambdaExpression = rewrittenLambdaBodyData.rewrittenLambdaExpression;

            // Go through all changed nodes and check to see if they are a component access method that we need to patch (GetComponent/SetComponent/etc)
            // Only need to do this if we are not doing structural changes (in which case we can't as structural changes will invalidate)
            if (!description.WithStructuralChanges)
            {
                foreach (var originalNode in rewrittenLambdaBodyData.thisAccessNodesNeedingReplacement)
                {
                    var originalInvocation = originalNode.Ancestors().OfType<InvocationExpressionSyntax>().First();

                    var currentNode = rewrittenLambdaExpression.GetCurrentNode(originalNode);
                    var currentNodeInvocationExpression = currentNode.Ancestors().OfType<InvocationExpressionSyntax>().FirstOrDefault();
                    if (currentNodeInvocationExpression == null)
                        continue;

                    var replacementMemberAccessExpression = lambdaBodyRewriter.GenerateReplacementMemberAccessExpression(description, originalInvocation, model);
                    if (replacementMemberAccessExpression != null)
                        rewrittenLambdaExpression = rewrittenLambdaExpression.ReplaceNode(currentNodeInvocationExpression, replacementMemberAccessExpression);
                }
            }

            // Go through all local declaration nodes and replace them with assignment nodes (or remove) if they are now captured variables that live in job struct
            // This is needed for variables captured for local methods
            foreach (var localDeclarationSyntax in rewrittenLambdaExpression.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
            {
                var variableDeclaration = localDeclarationSyntax.DescendantNodes().OfType<VariableDeclarationSyntax>().FirstOrDefault();
                if (variableDeclaration != null &&
                    variablesCapturedOnlyByLocals.Any(variable => variable.OriginalVariableName == variableDeclaration.Variables.First().Identifier.Text))
                {
                    if (variableDeclaration.DescendantTokens().Any(token => token.Kind() == SyntaxKind.EqualsToken))
                    {
                        var variableIdentifier = variableDeclaration.Variables.First().Identifier;
                        var nodeAfter = variableDeclaration.NodeAfter(node => node.Kind() == SyntaxKind.EqualsToken);
                        rewrittenLambdaExpression = rewrittenLambdaExpression.ReplaceNode(localDeclarationSyntax,
                                SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(
                                    SyntaxKind.SimpleAssignmentExpression,
                                    SyntaxFactory.IdentifierName(variableIdentifier.Text),
                                    (ExpressionSyntax)nodeAfter)));
                    }
                    else
                        rewrittenLambdaExpression = rewrittenLambdaExpression.RemoveNode(localDeclarationSyntax, SyntaxRemoveOptions.KeepExteriorTrivia);
                }
            }

            // Go through all local function statements and omit them as method declarations on the job struct
            // (local methods accessing fields on this are not allowed in C#)
            // https://sharplab.io/#v2:EYLgtghgzgLgpgJwDQxNGAfAAgJgIwCwAUFgMwAEu5AwuQN7HlOUVYAs5AsgBQCU9jZgF9BTUeVgIArgGMY5AKIA7GAEsYAT3oiizCTGlyaAezAAHY0rgqAKhrNwAEhCUATADZwAPDYB828UlZeWpTCysVABEIGAgAMQRTZTVNH386HT0gowUZKBs4WGjY5PUtcQZdPWYyRRUy8gA3CHcpODwAbnFM5mz5XPzCmGKIASrqlkU8gqKYiG5VFSaWtv4M7vFN8fJF+AQAMwgZOHIASQApY2BqAAspJQBrAO2+8gAZCDBgVwhL4AB9AAM/z+5BAZz+t3uDwq4j0tVC5ks1hgdgczjcni8AxmwzmpU0/n+MFccDRThcHjgXW28IoiPCKJGCSS9VSOKGIyJ/w5s1i/xZYAJGhpEzEtJqHAA8ghVABzRYtD5fH4AIWMrg03GF5BRZSQOyUU0GfIgOpJcF4cOYlTF1V25AA4nAYMKAGorODatlaS3Wia2u1irAAdnI/x5005cwFiSFPoA2nAALoAOmarWp/uqOmz8I4AGUXe7Pd6Ur6DQ6M6s8zbaxMI7y8fzBcKk8nyABechWADuxtxI241ctoqDTB64/r5CLrp9Hsz3D1mgNzrn5YXbW4FvTnrwvF4Y4muYlTAA9AAqbO1e5QCD7E7sRQADzgMik8G4AEEEDIbi77DgKFHnIP9oUrJYwMeU43DgZ8IPkfZVAQWBhRg0lnytU8xnHJhmgQXUfQABQMLtyDLMoL14KCHlTNcADkYlURo4B/BAIC1f5lw0ckMSpXg6JdABVJQ7wfAAlOAIFcKUlHcDQSIQPgjyDfDyAtL8ZGOKAoGMAju21KNTWFKiaMEmBGLUFi2I47hiVJXjKU8AS1xEsS4Ek6TZPkxTlOnB1uNCe55G7MygpUFS7X2PTyIdVQyMBDodnIABCbtAuMYKktUABqHKsNwnDCuYGV5UVdxlW+CB1U1bgLyXYjSJynZeANeqNK0wpdII5rVAPSKxUnHNswvM8NiIIQgA===
            var localFunctions = rewrittenLambdaExpression.DescendantNodes().OfType<LocalFunctionStatementSyntax>();
            rewrittenLambdaExpression = rewrittenLambdaExpression.RemoveNodes(localFunctions, SyntaxRemoveOptions.KeepNoTrivia);
            var methodsForLocalFunctions = new List<MethodDeclarationSyntax>();
            foreach (var localFunction in localFunctions)
                methodsForLocalFunctions.Add((MethodDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(localFunction.ToString()));

            return (rewrittenLambdaExpression, lambdaBodyRewriter.DataFromEntityFields.Values.ToList(), methodsForLocalFunctions);
        }

        SyntaxNode GenerateReplacementMemberAccessExpression(LambdaJobDescription description, SyntaxNode originalNode, SemanticModel model)
        {
            if (originalNode is InvocationExpressionSyntax originalInvocationNode)
            {
                if (model.GetSymbolInfo(originalInvocationNode.Expression).Symbol is IMethodSymbol methodSymbol &&
                    methodSymbol.ContainingType.Is("Unity.Entities.SystemBase") &&
                    methodSymbol.IsGenericMethod && methodSymbol.TypeArguments.Length == 1)
                {
                    var patchMethod = LambdaJobsPatchableMethod.PatchableMethods.FirstOrDefault(patchableMethod => methodSymbol.Name == patchableMethod.UnpatchedMethod);
                    if (patchMethod != null)
                    {
                        var readOnlyAccess = true;
                        switch (patchMethod.AccessRights)
                        {
                            case LambdaJobsPatchableMethod.ComponentAccessRights.ReadOnly:
                                readOnlyAccess = true;
                                break;
                            case LambdaJobsPatchableMethod.ComponentAccessRights.ReadWrite:
                                readOnlyAccess = false;
                                break;

                            // Get read-access from method's param
                            case LambdaJobsPatchableMethod.ComponentAccessRights.GetFromFirstMethodParam:
                                if (originalInvocationNode.ArgumentList.Arguments.Count == 0)
                                {
                                    // Default parameter value for GetComponentFromEntity/GetBufferFromEntity is false (aka `bool isReadOnly = false`)
                                    readOnlyAccess = false;
                                }
                                else
                                {
                                    var literalArgument = originalInvocationNode.ArgumentList.Arguments.FirstOrDefault()?.DescendantNodes().OfType<LiteralExpressionSyntax>().FirstOrDefault();
                                    if (literalArgument != null && description.SemanticModel.GetConstantValue(literalArgument).Value is bool boolValue)
                                    {
                                        readOnlyAccess = boolValue;
                                    }
                                    else
                                    {
                                        LambdaJobsErrors.DC0059(description.SystemGeneratorContext, description.Location, methodSymbol.Name);
                                        description.Success = false;
                                        throw new LambdaJobDescription.InvalidDescriptionException();
                                    }
                                }

                                break;
                        }

                        // If we have read/write access, we can only guaranteed safe access with sequential access (.Run or .Schedule)
                        if (!readOnlyAccess && description.Schedule.Mode == ScheduleMode.ScheduleParallel)
                        {
                            var patchedMethodTypeArgument = methodSymbol.TypeArguments.First();
                            LambdaJobsErrors.DC0063(description.SystemGeneratorContext, description.Location, methodSymbol.Name, patchedMethodTypeArgument.Name);
                            description.Success = false;
                            throw new LambdaJobDescription.InvalidDescriptionException();
                        }

                        // Make sure our componentDataFromEntityField doesn't give write access to a lambda parameter of the same type
                        // or there is a writable lambda parameter that gives access to this type (either could violate aliasing rules).
                        foreach (var parameter in description.LambdaParameters)
                        {
                            var patchedMethodTypeArgument = methodSymbol.TypeArguments.First();
                            if (parameter.TypeSymbol.ToFullName() == patchedMethodTypeArgument.ToFullName())
                            {
                                if (!readOnlyAccess)
                                {
                                    LambdaJobsErrors.DC0046(description.SystemGeneratorContext, description.Location, methodSymbol.Name, parameter.TypeSymbol.Name);
                                    description.Success = false;
                                    throw new LambdaJobDescription.InvalidDescriptionException();
                                }
                                else if (!parameter.QueryTypeIsReadOnly())
                                {
                                    LambdaJobsErrors.DC0047(description.SystemGeneratorContext, description.Location, methodSymbol.Name, parameter.TypeSymbol.Name);
                                    description.Success = false;
                                    throw new LambdaJobDescription.InvalidDescriptionException();
                                }
                            }
                        }

                        return patchMethod.GeneratePatchedReplacementSyntax(methodSymbol, this, originalInvocationNode);
                    }
                }
            }

            return null;
        }

        // Gets or created a field declaration for a type as needed.
        // This will first check if a RW one is available, if that is the case we should use that.
        // If not it will check to see if a RO one is available, use that and promote to RW if needed.
        // Finally, if we don't have one at all, let's create one with the appropriate access rights
        internal DataFromEntityFieldDescription GetOrCreateDataAccessField(ITypeSymbol type, bool asReadOnly, LambdaJobsPatchableMethod.AccessorDataType patchableMethodDataType)
        {
            if (DataFromEntityFields.TryGetValue(type, out var result))
            {
                if (result.IsReadOnly && !asReadOnly)
                    DataFromEntityFields[type] = new DataFromEntityFieldDescription(false, type, patchableMethodDataType);

                return DataFromEntityFields[type];
            }

            DataFromEntityFields[type] = new DataFromEntityFieldDescription(asReadOnly, type, patchableMethodDataType);
            return DataFromEntityFields[type];
        }
    }
}

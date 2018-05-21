﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Composition;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting.Rules;
using Microsoft.CodeAnalysis.Options;
using System.Linq;

namespace Microsoft.CodeAnalysis.CSharp.Formatting
{
    [ExportFormattingRule(Name, LanguageNames.CSharp), Shared]
    [ExtensionOrder(After = IndentBlockFormattingRule.Name)]
    internal class SuppressFormattingRule : BaseFormattingRule
    {
        internal const string Name = "CSharp Suppress Formatting Rule";

        public override void AddSuppressOperations(List<SuppressOperation> list, SyntaxNode node, SyntaxToken lastToken, OptionSet optionSet, NextAction<SuppressOperation> nextOperation)
        {
            nextOperation.Invoke(list);

            AddInitializerSuppressOperations(list, node);

            AddBraceSuppressOperations(list, node, lastToken);

            AddStatementExceptBlockSuppressOperations(list, node);

            AddSpecificNodesSuppressOperations(list, node, lastToken);
        }

        private void AddSpecificNodesSuppressOperations(List<SuppressOperation> list, SyntaxNode node, SyntaxToken lastToken)
        {
            if (node is IfStatementSyntax ifStatementNode)
            {
                AddSuppressWrappingIfOnSingleLineOperation(list, ifStatementNode.IfKeyword, ifStatementNode.Statement.GetLastToken(includeZeroWidth: true));

                if (ifStatementNode.Else != null)
                {
                    AddSuppressWrappingIfOnSingleLineOperation(list, ifStatementNode.Else.ElseKeyword, ifStatementNode.Else.Statement.GetLastToken(includeZeroWidth: true));
                }

                return;
            }

            // ex: `e is Type ( /* positional */ )`
            if (node is RecursivePatternSyntax recursivePattern)
            {
                var deconstruct = recursivePattern.DeconstructionPatternClause;
                var property = recursivePattern.PropertyPatternClause;
                if (deconstruct != null)
                {
                    // Formatting should refrain from inserting new lines, unless the user already split across multiple lines
                    AddSuppressWrappingIfOnSingleLineOperation(list, deconstruct.OpenParenToken, deconstruct.CloseParenToken);
                    if (property != null)
                    {
                        AddSuppressWrappingIfOnSingleLineOperation(list, deconstruct.OpenParenToken, property.GetLastToken());
                    }
                }

                // ex: `Property: <pattern>` inside a recursive pattern, such as `e is { Property: <pattern>, ... }`
                else if (property != null)
                {
                    // Formatting should refrain from inserting new lines, unless the user already split across multiple lines
                    AddSuppressWrappingIfOnSingleLineOperation(list, property.OpenBraceToken, property.CloseBraceToken);
                }

                return;
            }

            // ex: `<pattern>: expression` inside a switch expression, such as `e switch { <pattern>: expression, ... }`
            if (node is SwitchExpressionArmSyntax switchExpressionArm)
            {
                // Formatting should refrain from inserting new lines, unless the user already split across multiple lines
                AddSuppressWrappingIfOnSingleLineOperation(list, switchExpressionArm.GetFirstToken(), switchExpressionArm.GetLastToken());
                return;
            }

            // ex: `e switch { <pattern>: expression, ... }`
            if (node is SwitchExpressionSyntax switchExpression)
            {
                // Formatting should refrain from inserting new lines, unless the user already split across multiple lines
                AddSuppressWrappingIfOnSingleLineOperation(list, switchExpression.GetFirstToken(), switchExpression.GetLastToken());
                return;
            }

            // ex: `case <pattern>:` inside a switch statement
            if (node is CasePatternSwitchLabelSyntax casePattern)
            {
                // Formatting should refrain from inserting new lines, unless the user already split across multiple lines
                AddSuppressWrappingIfOnSingleLineOperation(list, casePattern.GetFirstToken(), casePattern.GetLastToken());
                return;
            }

            // ex: `expression is <pattern>`
            if (node is IsPatternExpressionSyntax isPattern)
            {
                // Formatting should refrain from inserting new lines, unless the user already split across multiple lines
                AddSuppressWrappingIfOnSingleLineOperation(list, isPattern.GetFirstToken(), isPattern.GetLastToken());

                if (isPattern.Pattern is RecursivePatternSyntax recursionInIsPattern)
                {
                    // ex:
                    // ```
                    // _ = expr is (1, 2) { }$$
                    // M();
                    // ```
                    // or:
                    // ```
                    // _ = expr is { }$$
                    // M();
                    // ```
                    if (recursionInIsPattern.PropertyPatternClause != null)
                    {
                        AddSuppressWrappingIfOnSingleLineOperation(list, isPattern.IsKeyword, recursionInIsPattern.PropertyPatternClause.GetLastToken());
                    }
                }

                return;
            }

            if (node is ConstructorInitializerSyntax constructorInitializerNode)
            {
                var constructorDeclarationNode = constructorInitializerNode.Parent as ConstructorDeclarationSyntax;
                if (constructorDeclarationNode?.Body != null)
                {
                    AddSuppressWrappingIfOnSingleLineOperation(list, constructorInitializerNode.ColonToken, constructorDeclarationNode.Body.CloseBraceToken);
                }

                return;
            }

            if (node is DoStatementSyntax whileStatementNode)
            {
                AddSuppressWrappingIfOnSingleLineOperation(list, whileStatementNode.GetFirstToken(includeZeroWidth: true), whileStatementNode.Statement.GetLastToken(includeZeroWidth: true));
                return;
            }

            if (node is MemberDeclarationSyntax memberDeclNode)
            {
                // Attempt to keep the part of a member that follows the attributes on a single
                // line if that's how it's currently written.
                var tokens = memberDeclNode.GetFirstAndLastMemberDeclarationTokensAfterAttributes();
                AddSuppressWrappingIfOnSingleLineOperation(list, tokens.Item1, tokens.Item2);

                // Also, If the member is on single line with its attributes on it, then keep 
                // it on a single line.  This is for code like the following:
                //
                //      [Import] public int Field1;
                //      [Import] public int Field2;
                var attributes = memberDeclNode.GetAttributes();
                var endToken = node.GetLastToken(includeZeroWidth: true);
                for (var i = 0; i < attributes.Count; ++i)
                {
                    AddSuppressWrappingIfOnSingleLineOperation(list,
                        attributes[i].GetFirstToken(includeZeroWidth: true),
                        endToken);
                }

                var propertyDeclNode = node as PropertyDeclarationSyntax;
                if (propertyDeclNode?.Initializer != null && propertyDeclNode?.AccessorList != null)
                {
                    AddSuppressWrappingIfOnSingleLineOperation(list, tokens.Item1, propertyDeclNode.AccessorList.GetLastToken());
                }

                return;
            }

            if (node is AccessorDeclarationSyntax accessorDeclNode)
            {
                AddSuppressWrappingIfOnSingleLineOperation(list, accessorDeclNode.Keyword, accessorDeclNode.GetLastToken(includeZeroWidth: true));
                return;
            }

            if (node is SwitchSectionSyntax switchSection)
            {
                if (switchSection.Labels.Count < 2)
                {
                    AddSuppressWrappingIfOnSingleLineOperation(list, switchSection.GetFirstToken(includeZeroWidth: true), switchSection.GetLastToken(includeZeroWidth: true));
                    return;
                }
                else
                {
                    // Add Separate suppression for each Label and for the last label, add the <> 
                    for (int i = 0; i < switchSection.Labels.Count - 1; ++i)
                    {
                        if (switchSection.Labels[i] != null)
                        {
                            AddSuppressWrappingIfOnSingleLineOperation(list, switchSection.Labels[i].GetFirstToken(includeZeroWidth: true), switchSection.Labels[i].GetLastToken(includeZeroWidth: true));
                        }
                    }

                    // For the last label add the rest of the statements of the switch
                    if (switchSection.Labels[switchSection.Labels.Count - 1] != null)
                    {
                        AddSuppressWrappingIfOnSingleLineOperation(list, switchSection.Labels[switchSection.Labels.Count - 1].GetFirstToken(includeZeroWidth: true), switchSection.GetLastToken(includeZeroWidth: true));
                    }

                    return;
                }
            }

            if (node is AnonymousFunctionExpressionSyntax ||
                node is LocalFunctionStatementSyntax)
            {
                AddSuppressWrappingIfOnSingleLineOperation(list,
                    node.GetFirstToken(includeZeroWidth: true),
                    node.GetLastToken(includeZeroWidth: true),
                    SuppressOption.IgnoreElastic);
                return;
            }

            if (node is ParameterSyntax parameterNode)
            {
                if (parameterNode.AttributeLists.Count != 0)
                {
                    var anchorToken = parameterNode.AttributeLists.First().OpenBracketToken;
                    AddSuppressAllOperationIfOnMultipleLine(list, anchorToken, parameterNode.GetLastToken());
                }
            }

            if (node is TryStatementSyntax tryStatement)
            {
                // Add a suppression operation if the try keyword and the block are in the same line
                if (!tryStatement.TryKeyword.IsMissing && tryStatement.Block != null && !tryStatement.Block.CloseBraceToken.IsMissing)
                {
                    AddSuppressWrappingIfOnSingleLineOperation(list, tryStatement.TryKeyword, tryStatement.Block.CloseBraceToken);
                }
            }

            if (node is CatchClauseSyntax catchClause)
            {
                // Add a suppression operation if the catch keyword and the corresponding block are in the same line
                if (!catchClause.CatchKeyword.IsMissing && catchClause.Block != null && !catchClause.Block.CloseBraceToken.IsMissing)
                {
                    AddSuppressWrappingIfOnSingleLineOperation(list, catchClause.CatchKeyword, catchClause.Block.CloseBraceToken);
                }
            }

            if (node is FinallyClauseSyntax finallyClause)
            {
                // Add a suppression operation if the finally keyword and the corresponding block are in the same line
                if (!finallyClause.FinallyKeyword.IsMissing && finallyClause.Block != null && !finallyClause.Block.CloseBraceToken.IsMissing)
                {
                    AddSuppressWrappingIfOnSingleLineOperation(list, finallyClause.FinallyKeyword, finallyClause.Block.CloseBraceToken);
                }
            }

            if (node is InterpolatedStringExpressionSyntax interpolatedStringExpression)
            {
                AddSuppressWrappingIfOnSingleLineOperation(list, interpolatedStringExpression.StringStartToken, interpolatedStringExpression.StringEndToken);
            }
        }

        private void AddStatementExceptBlockSuppressOperations(List<SuppressOperation> list, SyntaxNode node)
        {
            var statementNode = node as StatementSyntax;
            if (statementNode == null || statementNode.Kind() == SyntaxKind.Block)
            {
                return;
            }

            var firstToken = statementNode.GetFirstToken(includeZeroWidth: true);
            var lastToken = statementNode.GetLastToken(includeZeroWidth: true);

            AddSuppressWrappingIfOnSingleLineOperation(list, firstToken, lastToken);
        }

        private void AddInitializerSuppressOperations(List<SuppressOperation> list, SyntaxNode node)
        {
            // array or collection initializer case
            if (node.IsInitializerForArrayOrCollectionCreationExpression())
            {
                var arrayOrCollectionInitializer = node as InitializerExpressionSyntax;
                AddSuppressAllOperationIfOnMultipleLine(list, arrayOrCollectionInitializer.OpenBraceToken.GetPreviousToken(includeZeroWidth: true), arrayOrCollectionInitializer.CloseBraceToken);
                return;
            }

            var initializer = GetInitializerNode(node);
            if (initializer != null)
            {
                AddInitializerSuppressOperations(list, initializer.Parent, initializer.Expressions);
                return;
            }

            if (node is AnonymousObjectCreationExpressionSyntax anonymousCreationNode)
            {
                AddInitializerSuppressOperations(list, anonymousCreationNode, anonymousCreationNode.Initializers);
                return;
            }
        }

        private void AddInitializerSuppressOperations(List<SuppressOperation> list, SyntaxNode parent, IEnumerable<SyntaxNode> items)
        {
            // make creation node itself to not break into multiple line, if it is on same line
            AddSuppressWrappingIfOnSingleLineOperation(list, parent.GetFirstToken(includeZeroWidth: true), parent.GetLastToken(includeZeroWidth: true));

            // make each initializer expression to not break into multiple line if it is on same line
            foreach (var item in items)
            {
                var firstToken = item.GetFirstToken(includeZeroWidth: true);
                var lastToken = item.GetLastToken(includeZeroWidth: true);

                if (!firstToken.Equals(lastToken))
                {
                    AddSuppressWrappingIfOnSingleLineOperation(list, firstToken, lastToken);
                }
            }
        }

        private InitializerExpressionSyntax GetInitializerNode(SyntaxNode node)
        {
            switch (node)
            {
                case ObjectCreationExpressionSyntax objectCreationNode:
                    return objectCreationNode.Initializer;
                case ArrayCreationExpressionSyntax arrayCreationNode:
                    return arrayCreationNode.Initializer;
                case ImplicitArrayCreationExpressionSyntax implicitArrayNode:
                    return implicitArrayNode.Initializer;
            }

            return null;
        }
    }
}

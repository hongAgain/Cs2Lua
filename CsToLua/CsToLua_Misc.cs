﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Semantics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace RoslynTool.CsToLua
{
    internal partial class CsLuaTranslater
    {
        #region 基础遍历机制部分
        public void VisitToken(SyntaxToken token)
        {
            this.VisitLeadingTrivia(token, true);
            this.VisitTrailingTrivia(token, true);
        }
        public void VisitLeadingTrivia(SyntaxToken token, bool onlyComment)
        {
            bool hasLeadingTrivia = token.HasLeadingTrivia;
            if (hasLeadingTrivia) {
                SyntaxTriviaList.Enumerator enumerator = token.LeadingTrivia.GetEnumerator();
                while (enumerator.MoveNext()) {
                    SyntaxTrivia current = enumerator.Current;
                    this.VisitTrivia(current, onlyComment);
                }
            }
        }
        public void VisitTrailingTrivia(SyntaxToken token, bool onlyComment)
        {
            bool hasTrailingTrivia = token.HasTrailingTrivia;
            if (hasTrailingTrivia) {
                SyntaxTriviaList.Enumerator enumerator = token.TrailingTrivia.GetEnumerator();
                while (enumerator.MoveNext()) {
                    SyntaxTrivia current = enumerator.Current;
                    this.VisitTrivia(current, onlyComment);
                }
            }
        }
        public void VisitTrivia(SyntaxTrivia trivia, bool onlyComment)
        {
            bool flag = trivia.HasStructure;
            if (flag) {
                if (!onlyComment) {
                    this.Visit((CSharpSyntaxNode)trivia.GetStructure());
                }
            } else {
                if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)) {
                    string content = trivia.ToString();
                    content = content.Substring(2);
                    if (content != m_LastComment) {
                        m_LastComment = content;
                        CodeBuilder.AppendFormat("--{0}", content);
                        CodeBuilder.AppendLine();
                    }
                } else if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)) {
                    string content = trivia.ToString();
                    content = content.Substring(2, content.Length - 4);
                    if (content != m_LastComment) {
                        m_LastComment = content;
                        var lines = content.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines) {
                            CodeBuilder.AppendFormat("--{0}", line);
                            CodeBuilder.AppendLine();
                        }
                    }
                }
            }
        }
        public override void DefaultVisit(SyntaxNode node)
        {
            var nodes = node.ChildNodes();
            var enumer = nodes.GetEnumerator();
            while (enumer.MoveNext()) {
                this.Visit(enumer.Current);
            }
            var tokens = node.ChildTokens();
            var enumer2 = tokens.GetEnumerator();
            while (enumer2.MoveNext()) {
                this.VisitToken(enumer2.Current);
            }
        }
        public override void Visit(SyntaxNode node)
        {
            bool flag = node != null;
            if (flag) {
                if (node.HasLeadingTrivia && !(node is CompilationUnitSyntax)) {
                    SyntaxTriviaList.Enumerator enumerator = node.GetLeadingTrivia().GetEnumerator();
                    while (enumerator.MoveNext()) {
                        SyntaxTrivia current = enumerator.Current;
                        this.VisitTrivia(current, false);
                    }
                }
                ((CSharpSyntaxNode)node).Accept(this);
                m_LastComment = string.Empty;
                if (node.HasTrailingTrivia) {
                    SyntaxTriviaList.Enumerator enumerator = node.GetTrailingTrivia().GetEnumerator();
                    while (enumerator.MoveNext()) {
                        SyntaxTrivia current = enumerator.Current;
                        this.VisitTrivia(current, false);
                    }
                }
            }
        }
        #endregion

        #region 符号、变量、参数等相关的处理
        public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            base.VisitLocalDeclarationStatement(node);
        }
        public override void VisitVariableDeclaration(VariableDeclarationSyntax node)
        {
            foreach (var v in node.Variables) {
                VisitVariableDeclarator(v);
            }
        }
        public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
        {
            var ci = m_ClassInfoStack.Peek();
            var localSym = m_Model.GetDeclaredSymbol(node) as ILocalSymbol;

            if (null != node.Initializer) {
                CodeBuilder.AppendFormat("{0}local {1}; {2}", GetIndentString(), node.Identifier.Text, node.Identifier.Text);
            } else {
                CodeBuilder.AppendFormat("{0}local {1}", GetIndentString(), node.Identifier.Text);
            }
            if (null != localSym && localSym.HasConstantValue) {
                CodeBuilder.Append(" = ");
                OutputConstValue(localSym.ConstantValue, localSym);
                CodeBuilder.AppendLine(";");
                return;
            }
            VisitLocalVariableDeclarator(ci, node);
            if (null != node.Initializer) {
                var oper = m_Model.GetOperation(node.Initializer.Value);
                if (null != oper && null != oper.Type && oper.Type.TypeKind == TypeKind.Struct && oper.Type.ContainingAssembly == m_SymbolTable.AssemblySymbol) {
                    CodeBuilder.AppendFormat("{0}{1} = wrapvaluetype({2});", GetIndentString(), node.Identifier.Text, node.Identifier.Text);
                    CodeBuilder.AppendLine();
                }
            }
        }
        public override void VisitArgumentList(ArgumentListSyntax node)
        {
            var oper = m_Model.GetOperation(node);
            OutputArgumentList(node.Arguments, ", ", oper);
        }
        public override void VisitBracketedArgumentList(BracketedArgumentListSyntax node)
        {
            var oper = m_Model.GetOperation(node);
            if (oper.Kind == OperationKind.IndexedPropertyReferenceExpression) {
                OutputArgumentList(node.Arguments, ", ", oper);
            } else {
                OutputArgumentList(node.Arguments, "][", oper);
            }
        }
        public override void VisitArgument(ArgumentSyntax node)
        {
            var oper = m_Model.GetOperation(node) as IArgument;
            IConversionExpression opd = null;
            if (null != oper) {
                opd = oper.Value as IConversionExpression;
            }
            OutputExpressionSyntax(node.Expression, opd);
        }
        public override void VisitPredefinedType(PredefinedTypeSyntax node)
        {
            TypeInfo typeInfo = m_Model.GetTypeInfo(node);
            var type = typeInfo.Type;

            if (null != type) {
                string fullName = ClassInfo.GetFullName(type);
                CodeBuilder.Append(fullName);
            } else {
                CodeBuilder.Append(node.Keyword.Text);
            }
        }
        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            string name = node.Identifier.Text;
            if (m_ClassInfoStack.Count > 0) {
                ClassInfo classInfo = m_ClassInfoStack.Peek();
                SymbolInfo symbolInfo = m_Model.GetSymbolInfo(node);
                var sym = symbolInfo.Symbol;
                if (null != sym) {
                    if (sym.Kind == SymbolKind.NamedType || sym.Kind == SymbolKind.Namespace) {
                        string fullName = ClassInfo.GetFullName(sym);
                        CodeBuilder.Append(fullName);

                        if (sym.Kind == SymbolKind.NamedType) {
                            var namedType = sym as INamedTypeSymbol;
                            AddReferenceAndTryDeriveGenericTypeInstance(classInfo, namedType);
                        }
                        return;
                    } else if (sym.Kind == SymbolKind.Field || sym.Kind == SymbolKind.Property || sym.Kind == SymbolKind.Event) {
                        if (m_ObjectCreateStack.Count > 0) {
                            ITypeSymbol symInfo = m_ObjectCreateStack.Peek();
                            if (null != symInfo) {
                                var names = symInfo.GetMembers(name);
                                if (names.Length > 0) {
                                    CodeBuilder.AppendFormat("{0}", name);
                                    return;
                                }
                            }
                        }
                        if (sym.ContainingType == classInfo.SemanticInfo || sym.ContainingType == classInfo.SemanticInfo.OriginalDefinition || classInfo.IsInherit(sym.ContainingType)) {
                            if (sym.IsStatic) {
                                CodeBuilder.AppendFormat("{0}.{1}", classInfo.Key, sym.Name);
                            } else {
                                CodeBuilder.AppendFormat("this.{0}", sym.Name);
                            }
                            return;
                        }
                    } else if (sym.Kind == SymbolKind.Method && sym.ContainingType == classInfo.SemanticInfo) {
                        var msym = sym as IMethodSymbol;
                        string manglingName = NameMangling(msym);
                        var mi = new MethodInfo();
                        mi.Init(msym, node);
                        if (node.Parent is InvocationExpressionSyntax) {
                            if (sym.IsStatic) {
                                CodeBuilder.AppendFormat("{0}.{1}", classInfo.Key, manglingName);
                            } else {
                                CodeBuilder.AppendFormat("this:{0}", manglingName);
                            }
                        } else {
                            CodeBuilder.Append("(function(");
                            string paramsString = string.Join(", ", mi.ParamNames.ToArray());
                            CodeBuilder.Append(paramsString);
                            if (sym.IsStatic) {
                                CodeBuilder.AppendFormat(") {0}{1}.{2}({3}); end)", msym.ReturnsVoid ? string.Empty : "return ", classInfo.Key, manglingName, paramsString);
                            } else {
                                CodeBuilder.AppendFormat(") {0}this:{1}({2}); end)", msym.ReturnsVoid ? string.Empty : "return ", manglingName, paramsString);
                            }
                        }
                        return;
                    }
                } else {
                    if (m_ObjectCreateStack.Count > 0) {
                        ITypeSymbol symInfo = m_ObjectCreateStack.Peek();
                        if (null != symInfo) {
                            var names = symInfo.GetMembers(name);
                            if (names.Length > 0) {
                                CodeBuilder.AppendFormat("{0}", name);
                                return;
                            }
                        }
                    } else {
                        ReportIllegalSymbol(node, symbolInfo);
                    }
                }
            }
            CodeBuilder.Append(name);
        }
        public override void VisitQualifiedName(QualifiedNameSyntax node)
        {
            SymbolInfo symInfo = m_Model.GetSymbolInfo(node);
            var sym = symInfo.Symbol;
            if (null != sym) {
                string fullName = ClassInfo.GetFullName(sym);
                CodeBuilder.Append(fullName);
            } else {
                ReportIllegalSymbol(node, symInfo);
                CodeBuilder.Append(node.GetText());
            }
        }
        #endregion
    }
}
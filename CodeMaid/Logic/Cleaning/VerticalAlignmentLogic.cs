using EnvDTE;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SteveCadwallader.CodeMaid.Helpers;
using SteveCadwallader.CodeMaid.Model.CodeItems;
using SteveCadwallader.CodeMaid.Properties;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SteveCadwallader.CodeMaid.Logic.Cleaning
{
    internal class VerticalAlignmentLogic
    {
        #region Fields

        private readonly CodeMaidPackage _package;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// The singleton instance of the <see cref="VerticalAlignmentLogic" /> class.
        /// </summary>
        private static VerticalAlignmentLogic _instance;

        /// <summary>
        /// Gets an instance of the <see cref="VerticalAlignmentLogic" /> class.
        /// </summary>
        /// <param name="package">The hosting package.</param>
        /// <returns>An instance of the <see cref="VerticalAlignmentLogic" /> class.</returns>
        internal static VerticalAlignmentLogic GetInstance(CodeMaidPackage package)
        {
            return _instance ?? (_instance = new VerticalAlignmentLogic(package));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VerticalAlignmentLogic" /> class.
        /// </summary>
        /// <param name="package">The hosting package.</param>
        private VerticalAlignmentLogic(CodeMaidPackage package)
        {
            _package = package;
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Inserts space on adjacent assignment lines to align them vertically.
        /// </summary>
        /// <param name="textDocument">The text document to cleanup.</param>
        internal void VerticallyAlignAssignments(EnvDTE.TextDocument textDocument)
        {
            if (!Settings.Default.Cleaning_VerticallyAlignAfterAssignements) return;

            List<AssignmentOperator> foundAssignments = new List<AssignmentOperator>();

            var editPoint = textDocument.CreateEditPoint();
            editPoint.StartOfDocument();
            while (!editPoint.AtEndOfDocument)
            {
                editPoint.StartOfLine();
                AssignmentOperator foundOp;
                if (GetAssignmentOperator(editPoint.GetLine(), out foundOp))
                {
                    foundAssignments.Add(foundOp);
                }
                else
                {
                    if (foundAssignments.Count > 1)
                    {
                        PadAssignmentsToAlign(editPoint, foundAssignments);
                    }
                    foundAssignments.Clear();
                }

                editPoint.LineDown();
            }
        }

        /// <summary>
        /// Inserts space on adjacent variable declaration lines to align them vertically.
        /// </summary>
        /// <param name="textDocument">The text document to cleanup.</param>
        internal void VerticallyAlignAfterTypesAndModifiers(EnvDTE.TextDocument textDocument)
        {
            if (!Settings.Default.Cleaning_VerticallyAlignAfterTypesAndModifiers) return;

            var editPoint = textDocument.CreateEditPoint();
            editPoint.StartOfDocument();
            var end = editPoint.CreateEditPoint();
            end.EndOfDocument();
            string docText = editPoint.GetText(end);
            //EditPoint.GetText() replaces line endings.
            //Internally line endings are always a single char long.
            //This confuses the count if not compensated for.
            docText = docText.Replace("\r\n", "\n");
            var syntaxTree = CSharpSyntaxTree.ParseText(docText);
            var localNodes = ((CompilationUnitSyntax)syntaxTree.GetRoot()).DescendantNodes().OfType<VariableDeclarationSyntax>().ToList();
            List<int> absoluteStartPoints = new List<int>(localNodes.Count);
            foreach (var node in localNodes)
            {
                absoluteStartPoints.Add(node.Variables.First().Identifier.SpanStart);
            }
            absoluteStartPoints.Sort();

            int runningOffset = 0;
            int prevLine = -2;
            List<int> adjacentLineAbsoluteOffsets = new List<int>();
            foreach (int point in absoluteStartPoints)
            {
                //The document uses base 1 offsets while TextSpan uses base 0.
                //So the + 1 hangs around to compensate.
                editPoint.MoveToAbsoluteOffset(point + runningOffset + 1);
                int currentLine = editPoint.Line;
                int column = editPoint.LineCharOffset;
                if (currentLine == prevLine)
                {
                }
                else if (currentLine - 1 == prevLine)
                {
                    adjacentLineAbsoluteOffsets.Add(point + runningOffset + 1);
                }
                else
                {
                    if (adjacentLineAbsoluteOffsets.Count > 2)
                    {
                        runningOffset += PadVariablesToAlign(editPoint, adjacentLineAbsoluteOffsets);
                    }
                    adjacentLineAbsoluteOffsets.Clear();
                    adjacentLineAbsoluteOffsets.Add(point + runningOffset + 1);
                }
                prevLine = currentLine;
            }

            if (adjacentLineAbsoluteOffsets.Count > 2)
            {
                runningOffset += PadVariablesToAlign(editPoint, adjacentLineAbsoluteOffsets);
            }
        }

        /// <summary>
        /// Aligns variables starting points to the same column.
        /// </summary>
        /// <param name="editPoint">An editPoint in the document to modify.</param>
        /// <param name="points">A list of starting points for the variables to align.</param>
        /// <returns></returns>
        private int PadVariablesToAlign(EditPoint editPoint, List<int> points)
        {
            int targetColumn = 0;
            int runningOffset = 0;
            foreach (var point in points)
            {
                editPoint.MoveToAbsoluteOffset(point);
                targetColumn = Math.Max(targetColumn, editPoint.LineCharOffset);
            }
            foreach (var point in points)
            {
                editPoint.MoveToAbsoluteOffset(point + runningOffset);
                int padding = targetColumn - editPoint.LineCharOffset;
                for (int i = 0; i < padding; i++)
                {
                    editPoint.Insert(" ");
                    runningOffset++;
                }
            }
            return runningOffset;
        }

        #endregion Methods

        #region HelperMethods

        /// <summary>
        /// Vertically aligns assignment operations by inserting spaces in lines above the editPoint position.
        /// </summary>
        /// <param name="editPoint">An edit point on the line below the block of lines to align.</param>
        /// <param name="assignments">The assignment points ordered from top to bottom.</param>
        private void PadAssignmentsToAlign(EditPoint editPoint, List<AssignmentOperator> assignments)
        {
            int targetColumn = 0;
            int longestOperator = 0;
            foreach (AssignmentOperator a in assignments)
            {
                targetColumn = Math.Max(targetColumn, a.position);
                longestOperator = Math.Max(longestOperator, a.operatorLength);
            }

            for (int i = assignments.Count; i > 0; i--)
            {
                editPoint.LineUp();
            }

            foreach (AssignmentOperator a in assignments)
            {
                editPoint.StartOfLine();
                editPoint.CharRight(a.position);
                int padding = targetColumn - a.position + longestOperator - a.operatorLength;
                for (int i = 0; i < padding; i++)
                {
                    editPoint.Insert(" ");
                }
                editPoint.LineDown();
            }
        }

        /// <summary>
        /// Searches the line for a valid assignment operation and outputs an AssignmentOperator if successful.
        /// The algorithm ignores assignment operations if special characters precede the operator.
        /// In such cases, alignment is typically not desired.
        /// </summary>
        /// <param name="line">The line to search for an assignment</param>
        /// <param name="foundAssignment"></param>
        /// <returns></returns>
        private bool GetAssignmentOperator(string line, out AssignmentOperator foundAssignment)
        {
            foundAssignment = new AssignmentOperator { position = -1, operatorLength = 0 };
            List<AssignmentOperator> operators = new List<AssignmentOperator>();
            operators.Add(new AssignmentOperator { position = line.IndexOf(" = "), operatorLength = 1 });
            operators.Add(new AssignmentOperator { position = line.IndexOf(" += "), operatorLength = 2 });
            operators.Add(new AssignmentOperator { position = line.IndexOf(" *= "), operatorLength = 2 });
            operators.Add(new AssignmentOperator { position = line.IndexOf(" /= "), operatorLength = 2 });
            operators.Add(new AssignmentOperator { position = line.IndexOf(" %= "), operatorLength = 2 });
            operators.Add(new AssignmentOperator { position = line.IndexOf(" <<= "), operatorLength = 3 });
            operators.Add(new AssignmentOperator { position = line.IndexOf(" >>= "), operatorLength = 3 });
            operators.Add(new AssignmentOperator { position = line.IndexOf(" &= "), operatorLength = 2 });
            operators.Add(new AssignmentOperator { position = line.IndexOf(" |= "), operatorLength = 2 });
            operators.Add(new AssignmentOperator { position = line.IndexOf(" ^= "), operatorLength = 2 });
            int bestIndex = -1;
            for (int i = 0; i < operators.Count; i++)
            {
                if (operators[i].position > 0)
                {
                    if (bestIndex < 0 || operators[i].position < operators[bestIndex].position)
                    {
                        bestIndex = i;
                    }
                }
            }

            if (bestIndex < 0)
                return false;

            int ignoreCharStart = line.IndexOf('"');
            if (ignoreCharStart > 0 && ignoreCharStart < operators[bestIndex].position)
                return false;
            ignoreCharStart = line.IndexOf('(');
            if (ignoreCharStart > 0 && ignoreCharStart < operators[bestIndex].position)
                return false;
            ignoreCharStart = line.IndexOf("{");
            if (ignoreCharStart > 0 && ignoreCharStart < operators[bestIndex].position)
                return false;
            ignoreCharStart = line.IndexOf("//");
            if (ignoreCharStart > 0 && ignoreCharStart < operators[bestIndex].position)
                return false;

            foundAssignment = operators[bestIndex];

            return true;
        }

        #endregion HelperMethods

        #region Structs

        /// <summary>
        /// Holds relevant information about an assignment on a line needed for the algorithms.
        /// </summary>
        private struct AssignmentOperator
        {
            public int position;
            public int operatorLength;
        }

        #endregion Structs
    }
}
using Auxil.CodeGeneration.Common.Languages;
using Auxil.CodeGeneration.Common.Rendering;

namespace Auxil.CodeGeneration.Common.Builders;

/// <summary>
/// The code builder entry point. One builder, one language: the language
/// is a type parameter, so the compiler only offers the features that
/// language supports.
///
/// File() returns a typed FileScope; every construct returns its own
/// typed scope, so the compiler only offers valid next steps, and scope
/// misuse becomes a compile-time error instead of a runtime one.
/// </summary>
[PublicAPI]
public class CodeBuilder<TLanguage>
    where TLanguage : struct, ILanguage
{
    private bool _fileOpen;

    /// <summary>Creates a builder for the given language.</summary>
    /// <param name="options">Formatting options; defaults are used when null.</param>
    public CodeBuilder(CodeBuilderOptions? options = null)
    {
        Options = options ?? new CodeBuilderOptions();
    }

    /// <summary>The formatting options in effect.</summary>
    public CodeBuilderOptions Options { get; }

    /// <summary>The model being built, or null before File() is called.</summary>
    public CodeFile? Model { get; private set; }

    /// <summary>Warnings collected by the most recent render.</summary>
    public List<string> Warnings { get; } = [];

    /// <summary>Starts a new file and returns its file scope.</summary>
    /// <param name="fileName">The file name, used for the model only.</param>
    /// <exception cref="CodeBuilderException">A file is already open.</exception>
    public FileScope<TLanguage> File(string fileName)
    {
        if (Model is not null)
        {
            throw new CodeBuilderException("A file is already open.");
        }

        Model = new CodeFile { FileName = fileName };
        _fileOpen = true;
        return new FileScope<TLanguage>(this, Model);
    }

    /// <summary>
    /// Replaces the statement with the given Id by one or more new
    /// statements. The replacement may be of a different kind than the
    /// original (e.g. replace a raw line with an If statement).
    /// </summary>
    public CodeBuilder<TLanguage> ReplaceStatement(
        string id,
        Action<BodyBuilder<TLanguage>> configure
    )
    {
        List<CodeStatement>? parent = FindStatementParent(id)
            ?? throw new CodeBuilderException($"No statement with id '{id}' found.");
        int index = parent.FindIndex(s => s.Id == id);
        parent.RemoveAt(index);

        BodyBuilder<TLanguage> bodyBuilder = new([]);
        configure(bodyBuilder);
        parent.InsertRange(index, bodyBuilder.Statements);
        return this;
    }

    /// <summary>Inserts new statements before the statement with the given Id.</summary>
    public CodeBuilder<TLanguage> InsertBefore(string id, Action<BodyBuilder<TLanguage>> configure)
    {
        List<CodeStatement>? parent = FindStatementParent(id)
            ?? throw new CodeBuilderException($"No statement with id '{id}' found.");
        int index = parent.FindIndex(s => s.Id == id);
        BodyBuilder<TLanguage> bodyBuilder = new([]);
        configure(bodyBuilder);
        parent.InsertRange(index, bodyBuilder.Statements);
        return this;
    }

    /// <summary>Removes the statement with the given Id.</summary>
    public CodeBuilder<TLanguage> Remove(string id)
    {
        List<CodeStatement>? parent = FindStatementParent(id)
            ?? throw new CodeBuilderException($"No statement with id '{id}' found.");
        parent.RemoveAt(parent.FindIndex(s => s.Id == id));
        return this;
    }

    /// <summary>Renders the model in the target language.</summary>
    public string Render()
    {
        if (Model is null)
        {
            throw new CodeBuilderException("No file open. Call File() first.");
        }

        if (_fileOpen)
        {
            throw new CodeBuilderException("File is still open. Call EndFile() before rendering.");
        }

        Warnings.Clear();
        return CodeRenderer.Render<TLanguage>(Model, Options, Warnings);
    }

    /// <summary>Renders the model to a string in the target language.</summary>
    public override string ToString()
    {
        return Render();
    }

    internal void CloseFile()
    {
        _fileOpen = false;
    }

    private List<CodeStatement>? FindStatementParent(string id)
    {
        return Model is null ? null : FindStatementParentInNode(Model, id);
    }

    private static List<CodeStatement>? FindStatementParentInNode(CodeNode node, string id)
    {
        return node switch
        {
            CodeFile file => FindStatementParentInChildren(file.Children, id),
            CodeNamespace namespaceNode => FindStatementParentInChildren(
                namespaceNode.Children,
                id
            ),
            CodeClass cls => FindStatementParentInChildren(cls.Members, id),
            CodeInterface iface => FindStatementParentInChildren(iface.Members, id),
            CodeMethod method => FindStatementParentInBody(method.Body, id),
            CodeConstructor ctor => FindStatementParentInBody(ctor.Body, id),
            CodeFunction function => FindStatementParentInBody(function.Body, id),
            CodeProperty property => FindStatementParentInBody(property.GetterBody, id)
                ?? FindStatementParentInBody(property.SetterBody, id),
            _ => null,
        };
    }

    private static List<CodeStatement>? FindStatementParentInChildren(
        IEnumerable<CodeNode> children,
        string id
    )
    {
        foreach (CodeNode child in children)
        {
            List<CodeStatement>? found = FindStatementParentInNode(child, id);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static List<CodeStatement>? FindStatementParentInBody(CodeBody? body, string id)
    {
        return body is null ? null : FindStatementParentInStatements(body.Statements, id);
    }

    private static List<CodeStatement>? FindStatementParentInStatements(
        List<CodeStatement> statements,
        string id
    )
    {
        if (statements.Any(s => s.Id == id))
        {
            return statements;
        }

        foreach (CodeStatement statement in statements)
        {
            List<CodeStatement>? found = statement switch
            {
                CodeIf ifStatement => FindStatementParentInStatements(ifStatement.Then, id)
                    ?? FindInElseIfs(ifStatement.ElseIfs, id)
                    ?? FindStatementParentInStatements(ifStatement.Else, id),
                CodeForEach forEach => FindStatementParentInStatements(forEach.Body, id),
                CodeWhile whileStatement => FindStatementParentInStatements(
                    whileStatement.Body,
                    id
                ),
                CodeDoWhile doWhile => FindStatementParentInStatements(doWhile.Body, id),
                CodeFor forStatement => FindStatementParentInStatements(forStatement.Body, id),
                CodeRawBlock rawBlock => FindStatementParentInStatements(rawBlock.Body, id),
                CodeMatch match => FindInMatchArms(match.Arms, id),
                CodeTry tryStatement => FindStatementParentInStatements(tryStatement.Body, id)
                    ?? FindInCatches(tryStatement.Catches, id)
                    ?? FindStatementParentInStatements(tryStatement.Finally, id),
                CodeNestedFunction nestedFunction => FindStatementParentInStatements(
                    nestedFunction.Body?.Statements ?? [],
                    id
                ),
                CodeObjectLiteral literal => FindInObjectMembers(literal.Members, id),
                _ => null,
            };

            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static List<CodeStatement>? FindInElseIfs(List<CodeElseIf> elseIfs, string id)
    {
        foreach (CodeElseIf elseIf in elseIfs)
        {
            List<CodeStatement>? found = FindStatementParentInStatements(elseIf.Body, id);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static List<CodeStatement>? FindInMatchArms(List<CodeMatchArm> arms, string id)
    {
        foreach (CodeMatchArm arm in arms)
        {
            List<CodeStatement>? found = FindStatementParentInStatements(arm.Body, id);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static List<CodeStatement>? FindInCatches(List<CodeCatch> catches, string id)
    {
        foreach (CodeCatch catchBlock in catches)
        {
            List<CodeStatement>? found = FindStatementParentInStatements(catchBlock.Body, id);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static List<CodeStatement>? FindInObjectMembers(
        List<CodeObjectMember> members,
        string id
    )
    {
        foreach (CodeObjectMember member in members)
        {
            if (member.Function is not null)
            {
                List<CodeStatement>? found = FindStatementParentInStatements(
                    member.Function.Body?.Statements ?? [],
                    id
                );
                if (found is not null)
                {
                    return found;
                }
            }

            if (member.Object is not null)
            {
                List<CodeStatement>? found = FindInObjectMembers(member.Object.Members, id);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }
}

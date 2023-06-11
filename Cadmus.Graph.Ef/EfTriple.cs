namespace Cadmus.Graph.Ef;

public class EfTriple : Triple
{
    public EfNode? Subject { get; set; }
    public EfNode? Predicate { get; set; }
    public EfNode? Object { get; set; }

    public EfTriple()
    {
    }

    public EfTriple(Triple triple)
    {
        if (triple is null) throw new ArgumentNullException(nameof(triple));

        Id = triple.Id;
        SubjectId = triple.SubjectId;
        PredicateId = triple.PredicateId;
        ObjectId = triple.ObjectId;
        ObjectLiteral = triple.ObjectLiteral;
        ObjectLiteralIx = triple.ObjectLiteralIx;
        LiteralType = triple.LiteralType;
        LiteralLanguage = triple.LiteralLanguage;
        LiteralNumber = triple.LiteralNumber;
        Sid = triple.Sid;
        Tag = triple.Tag;
    }

    public UriTriple ToUriTriple()
    {
        return new UriTriple
        {
            Id = Id,
            SubjectId = SubjectId,
            PredicateId = PredicateId,
            ObjectId = ObjectId,
            ObjectLiteral = ObjectLiteral,
            ObjectLiteralIx = ObjectLiteralIx,
            LiteralType = LiteralType,
            LiteralLanguage = LiteralLanguage,
            LiteralNumber = LiteralNumber,
            Sid = Sid,
            Tag = Tag,
            SubjectUri = Subject?.UriEntry?.Uri,
            PredicateUri = Predicate?.UriEntry?.Uri,
            ObjectUri = Object?.UriEntry?.Uri
        };
    }
}

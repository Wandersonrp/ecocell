using Ecocell.Communication.Enums.Document;
using Ecocell.Communication.Enums.User;

namespace Ecocell.Domain.Entities;

public class Document
{
    public Guid DocumentId { get; set; } = Guid.NewGuid();
    public DocumentType DocumentType { get; set; }
    public string Text { get; set; } = string.Empty;    
}

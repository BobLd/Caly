using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Caly.Core.Models
{
    public sealed record PdfBookmarkNode
    {
        public ObservableCollection<PdfBookmarkNode>? Nodes { get; }

        public string Title { get; }

        public int? PageNumber { get; }

        public PdfBookmarkNode(string title, int? pageNumber, IEnumerable<PdfBookmarkNode>? children)
        {
            Title = title;
            PageNumber = pageNumber;
            if (children is not null)
            {
                Nodes = new ObservableCollection<PdfBookmarkNode>(children);
            }
        }
    }
}

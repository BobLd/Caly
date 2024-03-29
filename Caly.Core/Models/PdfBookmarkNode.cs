﻿// Copyright (C) 2024 BobLd
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY - without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

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

// Copyright (C) 2024 BobLd
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

using System.Collections.ObjectModel;
using System.Diagnostics;
using Caly.Pdf.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Caly.Core.ViewModels
{
    [DebuggerDisplay("Page {PageNumber} Word index: {WordIndex} ({Score}), Children {Nodes?.Count}")]
    public sealed partial class TextSearchResultViewModel : ViewModelBase
    {
        [ObservableProperty] private SearchResultItemType _itemType;
        [ObservableProperty] private int _pageNumber;
        [ObservableProperty] private int? _wordIndex;
        [ObservableProperty] private PdfWord? _word;
        [ObservableProperty] private double? _score;
        [ObservableProperty] private ObservableCollection<TextSearchResultViewModel>? _nodes;

        public override string ToString()
        {
            if (Nodes is null)
            {
                return $"{WordIndex} [{ItemType}]";
            }
            return $"{PageNumber} ({Nodes.Count})";
        }
    }

    public enum SearchResultItemType
    {
        Unspecified = 0,
        Word = 1,
        Annotation = 2,
    }
}

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

namespace Caly.Pdf.Models
{
    public sealed class PdfDocumentProperties
    {
        /// <summary>
        /// The Pdf version.
        /// </summary>
        public required string PdfVersion { get; init; }

        /// <summary>
        /// The title of this document if applicable.
        /// </summary>
        public string? Title { get; init; }

        /// <summary>
        /// The name of the person who created this document if applicable.
        /// </summary>
        public string? Author { get; init; }

        /// <summary>
        /// The subject of this document if applicable.
        /// </summary>
        public string? Subject { get; init; }

        /// <summary>
        /// Any keywords associated with this document if applicable.
        /// </summary>
        public string? Keywords { get; init; }

        /// <summary>
        /// The name of the application which created the original document before it was converted to PDF if applicable.
        /// </summary>
        public string? Creator { get; init; }

        /// <summary>
        /// The name of the application used to convert the original document to PDF if applicable.
        /// </summary>
        public string? Producer { get; init; }

        /// <summary>
        /// The date and time the document was created.
        /// </summary>
        public string? CreationDate { get; init; }

        /// <summary>
        /// The date and time the document was most recently modified.
        /// </summary>
        public string? ModifiedDate { get; init; }

        public bool IsLinearised { get; init; }

        /// <summary>
        /// Other information.
        /// </summary>
        public IReadOnlyDictionary<string, string>? Others { get; init; }
    }
}

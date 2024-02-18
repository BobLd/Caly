﻿using Caly.Pdf.Models;
using Caly.Pdf.PageFactories.Helpers;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Filters;
using UglyToad.PdfPig.Geometry;
using UglyToad.PdfPig.Logging;
using UglyToad.PdfPig.Outline.Destinations;
using UglyToad.PdfPig.Parser;
using UglyToad.PdfPig.Tokenization.Scanner;
using UglyToad.PdfPig.Tokens;

namespace Caly.Pdf.PageFactories
{
    public sealed class PageInformationOptimisedFactory : IPageFactory<PdfPageInformation>
    {
        /// <summary>
        /// The parsing options.
        /// </summary>
        private readonly ParsingOptions parsingOptions;

        /// <summary>
        /// The Pdf token scanner.
        /// </summary>
        private readonly IPdfTokenScanner pdfScanner;

        /// <summary>
        /// Create a <see cref="BasePageFactory{TPage}"/>.
        /// </summary>
        public PageInformationOptimisedFactory(
            IPdfTokenScanner pdfScanner,
#pragma warning disable IDE0060 Roslyn.RCS1163
            IResourceStore resourceStore,
            ILookupFilterProvider filterProvider,
            IPageContentParser pageContentParser,
#pragma warning restore Roslyn.RCS1163 IDE0060
            ParsingOptions parsingOptions)
        {
            this.pdfScanner = pdfScanner;
            this.parsingOptions = parsingOptions;
        }

        /// <inheritdoc/>
        public PdfPageInformation Create(int number, DictionaryToken dictionary, PageTreeMembers pageTreeMembers, NamedDestinations namedDestinations)
        {
            if (dictionary == null)
            {
                throw new ArgumentNullException(nameof(dictionary));
            }

            var type = dictionary.GetNameOrDefault(NameToken.Type);

            if (type != null && !type.Equals(NameToken.Page))
            {
                parsingOptions.Logger.Error($"Page {number} had its type specified as {type} rather than 'Page'.");
            }

            var rotation = new PageRotationDegrees(pageTreeMembers.Rotation);
            if (dictionary.TryGet(NameToken.Rotate, pdfScanner, out NumericToken rotateToken))
            {
                rotation = new PageRotationDegrees(rotateToken.Int);
            }

            MediaBox mediaBox = GetMediaBox(number, dictionary, pageTreeMembers);
            CropBox cropBox = GetCropBox(dictionary, mediaBox);

            var initialMatrix = GetInitialMatrix(GetUserSpaceUnits(dictionary), mediaBox, cropBox, rotation, parsingOptions.Logger);

            ApplyTransformNormalise(initialMatrix, ref mediaBox, ref cropBox);

            // Special case where cropbox is outside mediabox: use cropbox instead of intersection
            var effectiveCropBox = mediaBox.Bounds.Intersect(cropBox.Bounds) ?? cropBox.Bounds;

            return new PdfPageInformation()
            {
                PageNumber = number,
                Width = effectiveCropBox.Width,
                Height = effectiveCropBox.Height
            };
        }

        /// <summary>
        /// Get the user space units.
        /// </summary>
        private static int GetUserSpaceUnits(DictionaryToken dictionary)
        {
            if (dictionary.TryGet(NameToken.UserUnit, out var userUnitBase) && userUnitBase is NumericToken userUnitNumber)
            {
                return userUnitNumber.Int;
            }

            return UserSpaceUnit.Default.PointMultiples;
        }

        /// <summary>
        /// Get the crop box.
        /// </summary>
        private CropBox GetCropBox(DictionaryToken dictionary, MediaBox mediaBox)
        {
            CropBox cropBox;
            if (dictionary.TryGet(NameToken.CropBox, out var cropBoxObject) &&
                DirectObjectFinderCaly.TryGet(cropBoxObject, pdfScanner, out ArrayToken cropBoxArray))
            {
                if (cropBoxArray.Length != 4)
                {
                    parsingOptions.Logger.Error(
                        $"The CropBox was the wrong length in the dictionary: {dictionary}. Array was: {cropBoxArray}. Using MediaBox.");

                    cropBox = new CropBox(mediaBox.Bounds);

                    return cropBox;
                }

                cropBox = new CropBox(cropBoxArray.ToRectangle(pdfScanner));
            }
            else
            {
                cropBox = new CropBox(mediaBox.Bounds);
            }

            return cropBox;
        }

        /// <summary>
        /// Get the media box.
        /// </summary>
        private MediaBox GetMediaBox(int number, DictionaryToken dictionary, PageTreeMembers pageTreeMembers)
        {
            MediaBox mediaBox;
            if (dictionary.TryGet(NameToken.MediaBox, out var mediaBoxObject)
                && DirectObjectFinderCaly.TryGet(mediaBoxObject, pdfScanner, out ArrayToken mediaBoxArray))
            {
                if (mediaBoxArray.Length != 4)
                {
                    parsingOptions.Logger.Error(
                        $"The MediaBox was the wrong length in the dictionary: {dictionary}. Array was: {mediaBoxArray}. Defaulting to US Letter.");

                    mediaBox = MediaBox.Letter;

                    return mediaBox;
                }

                mediaBox = new MediaBox(mediaBoxArray.ToRectangle(pdfScanner));
            }
            else
            {
                mediaBox = pageTreeMembers.MediaBox;

                if (mediaBox == null)
                {
                    parsingOptions.Logger.Error(
                        $"The MediaBox was the wrong missing for page {number}. Using US Letter.");

                    // PDFBox defaults to US Letter.
                    mediaBox = MediaBox.Letter;
                }
            }

            return mediaBox;
        }

        /// <summary>
        /// Apply the matrix transform to the media box and crop box.
        /// Then Normalise() in order to obtain rectangles with rotation=0
        /// and width and height as viewed on screen.
        /// </summary>
        private static void ApplyTransformNormalise(TransformationMatrix transformationMatrix, ref MediaBox mediaBox, ref CropBox cropBox)
        {
            if (transformationMatrix != TransformationMatrix.Identity)
            {
                mediaBox = new MediaBox(transformationMatrix.Transform(mediaBox.Bounds).Normalise());
                cropBox = new CropBox(transformationMatrix.Transform(cropBox.Bounds).Normalise());
            }
        }

        // From OperationContextHelper
        /// <summary>
        /// Get the initial transformation matrix.
        /// </summary>
        /// <param name="userSpaceUnit">User space unit.</param>
        /// <param name="mediaBox">The Media box as define in the document, without any applied transform.</param>
        /// <param name="cropBox">The Crop box as define in the document, without any applied transform.</param>
        /// <param name="rotation">The page rotation.</param>
        /// <param name="log"></param>
        [System.Diagnostics.Contracts.Pure]
        private static TransformationMatrix GetInitialMatrix(int pointMultiples,
            MediaBox mediaBox,
            CropBox cropBox,
            PageRotationDegrees rotation,
            ILog log)
        {
            // Cater for scenario where the cropbox is larger than the mediabox.
            // If there is no intersection (method returns null), fall back to the cropbox.
            var viewBox = mediaBox.Bounds.Intersect(cropBox.Bounds) ?? cropBox.Bounds;

            if (rotation.Value == 0
                && viewBox.Left == 0
                && viewBox.Bottom == 0
                && pointMultiples == 1)
            {
                return TransformationMatrix.Identity;
            }

            // Move points so that (0,0) is equal to the viewbox bottom left corner.
            var t1 = TransformationMatrix.GetTranslationMatrix(-viewBox.Left, -viewBox.Bottom);

            if (pointMultiples != 1)
            {
                log.Warn("User space unit other than 1 is not implemented");
            }

            // After rotating around the origin, our points will have negative x/y coordinates.
            // Fix this by translating them by a certain dx/dy after rotation based on the viewbox.
            double dx, dy;
            switch (rotation.Value)
            {
                case 0:
                    // No need to rotate / translate after rotation, just return the initial
                    // translation matrix.
                    return t1;
                case 90:
                    // Move rotated points up by our (unrotated) viewbox width
                    dx = 0;
                    dy = viewBox.Width;
                    break;
                case 180:
                    // Move rotated points up/right using the (unrotated) viewbox width/height
                    dx = viewBox.Width;
                    dy = viewBox.Height;
                    break;
                case 270:
                    // Move rotated points right using the (unrotated) viewbox height
                    dx = viewBox.Height;
                    dy = 0;
                    break;
                default:
                    throw new InvalidOperationException($"Invalid value for page rotation: {rotation.Value}.");
            }

            // GetRotationMatrix uses counter clockwise angles, whereas our page rotation
            // is a clockwise angle, so flip the sign.
            var r = TransformationMatrix.GetRotationMatrix(-rotation.Value);

            // Fix up negative coordinates after rotation
            var t2 = TransformationMatrix.GetTranslationMatrix(dx, dy);

            // Now get the final combined matrix T1 > R > T2
            return t1.Multiply(r.Multiply(t2));
        }
    }
}

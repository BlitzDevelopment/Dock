using Blitz;
using Rendering;
using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace DockMvvmSample.Services
{
    public class RenderingService : IDisposable
    {
        private static RenderingService _instance;
        private SVGRenderer _renderer;
        private static readonly object _lock = new object();
        private bool _disposed = false;

        private RenderingService() { }

        public static RenderingService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new RenderingService();
                    }
                }
                return _instance;
            }
        }

        public void Initialize(CsXFL.Document workingCsXFLDoc)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RenderingService));

            // Replace existing renderer if any
            _renderer = null;

            string appDataFolder = App.BlitzAppData.GetTmpFolder();
            _renderer = new SVGRenderer(workingCsXFLDoc, appDataFolder, true);
        }

        public SVGRenderer Renderer
        {
            get
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(RenderingService));
                
                if (_renderer == null)
                    throw new InvalidOperationException("RenderingService not initialized. Call Initialize() first.");
                
                return _renderer;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _renderer = null;
                _disposed = true;
            }
        }

        public static void Reset()
        {
            lock (_lock)
            {
                _instance?.Dispose();
                _instance = null;
            }
        }

        public CsXFL.Rectangle GetElementBBox(CsXFL.Element element, int frameIndex = 0, bool transformBBoxByMatrix = true)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RenderingService));

            if (_renderer == null)
                throw new InvalidOperationException("RenderingService not initialized. Call Initialize() first.");

            return _renderer.GetElementBoundingBox(element, frameIndex, transformBBoxByMatrix);
        }
        
        public XDocument RenderElementAsSvg(CsXFL.Element element, string elementIdentifier = null, int frameIndex = 0, CsXFL.Color? color = null, bool insideMask = false, bool returnIdentityTransformation = true)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RenderingService));

            if (_renderer == null)
                throw new InvalidOperationException("RenderingService not initialized. Call Initialize() first.");

            // Use default color if none provided
            var elementColor = color ?? CsXFL.Color.DefaultColor();

            // Hash the element as an identifier if none provided
            var elementId = elementIdentifier ?? element.GetHashCode().ToString();

            // Render the element
            (Dictionary<string, XElement> d, List<XElement> b) = _renderer.RenderElement(
                element,
                elementId,
                frameIndex,
                elementColor,
                insideMask: insideMask,
                returnIdentityTransformation: returnIdentityTransformation
            );

            // Create the root SVG element
            XNamespace svgNamespace = "http://www.w3.org/2000/svg";
            var svgRoot = new XElement(svgNamespace + "svg",
                new XAttribute("xmlns", svgNamespace.NamespaceName),
                new XAttribute("version", "1.1"),
                new XAttribute("width", "100%"),
                new XAttribute("height", "100%")
            );

            // Add the defs (d) to the SVG
            if (d != null && d.Count > 0)
            {
                var defsElement = new XElement(svgNamespace + "defs");
                foreach (var def in d.Values)
                {
                    defsElement.Add(def);
                }
                svgRoot.Add(defsElement);
            }

            // Add the body (b) to the SVG
            if (b != null && b.Count > 0)
            {
                foreach (var bodyElement in b)
                {
                    svgRoot.Add(bodyElement);
                }
            }

            // Create and return the XDocument
            return new XDocument(svgRoot);
        }

    }
}
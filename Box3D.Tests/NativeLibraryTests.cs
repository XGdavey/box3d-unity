using NUnit.Framework;

namespace Box3D.Tests
{
    public class NativeLibraryTests
    {
        [Test]
        public void GetVersion_MatchesPinnedNativeBuild()
        {
            // Update these on box3d version bumps (see Box3D.Native~/VERSION).
            Box3DVersion version = Box3DApi.GetVersion();

            Assert.AreEqual(0, version.Major);
            Assert.AreEqual(1, version.Minor);
            Assert.AreEqual(0, version.Revision);
        }

        [Test]
        public void NativeBuild_IsSinglePrecision()
        {
            Assert.IsFalse(Box3DApi.IsDoublePrecision);
        }
    }
}

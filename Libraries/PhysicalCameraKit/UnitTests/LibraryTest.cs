using Sandbox;

[TestClass]
public partial class LibraryTests
{
	[TestMethod]
	public void EquivalentFocalLengthProducesExpectedFieldOfView()
	{
		var fieldOfView = PhysicalCameraMath.HorizontalFieldOfView( 24.0f, 36.0f );

		Assert.AreEqual( 73.74f, fieldOfView, 0.01f );
	}

	[TestMethod]
	public void SboxUnitConversionRoundTripsMillimeters()
	{
		var units = PhysicalCameraMath.MillimetersToSboxUnits( 25.4f );

		Assert.AreEqual( 1.0f, units, 0.0001f );
		Assert.AreEqual( 25.4f, PhysicalCameraMath.SboxUnitsToMillimeters( units ), 0.0001f );
	}

	[TestMethod]
	public void ExposureValueMatchesSunnySixteenReference()
	{
		var exposureValue = PhysicalCameraMath.ExposureValue100( 16.0f, 1.0f / 125.0f );

		Assert.AreEqual( 15.0f, exposureValue, 0.05f );
	}

	[TestMethod]
	public void DepthOfFieldLimitsBracketFocusDistance()
	{
		var circleOfConfusion = PhysicalCameraMath.CircleOfConfusionMm( 9.8f, 7.35f );

		PhysicalCameraMath.DepthOfFieldLimitsMm( 6.53f, 1.78f, circleOfConfusion, 2000.0f, out var nearMm, out var farMm );

		Assert.IsTrue( nearMm < 2000.0f );
		Assert.IsTrue( farMm > 2000.0f );
	}

	[TestMethod]
	public void SceneTest()
	{
		var scene = new Scene();
		using ( scene.Push() )
		{
			var go = new GameObject();

			Assert.AreEqual( 1, scene.Directory.GameObjectCount );
		}
	}

}

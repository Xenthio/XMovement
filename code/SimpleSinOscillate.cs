using Sandbox;
using System;
using System.Threading;

public sealed class SimpleSinOscillate : Component
{
	[Property] public float Amplitude { get; set; } = 30;
	[Property] public float Period { get; set; } = 3;
	[Property] public bool Synchronized { get; set; } // if true, derives rotation from Synced Roundtime

	const float lerpRate = 15;

	//GameStateController gsc;
	float processedTime = 0;

	Vector3 currentLocalPosition;
	float startingZ = 0f;
	float t;

	float sinProgress = 0f;
	protected override void OnUpdate()
	{
		sinProgress += (Time.Delta / Period);

		currentLocalPosition = GameObject.LocalPosition;
		currentLocalPosition = new Vector3( currentLocalPosition.x, currentLocalPosition.y, startingZ + (MathF.Sin( sinProgress ) * Amplitude) );
		GameObject.LocalPosition = currentLocalPosition;
		/*
		if ( gsc == null )
		{
			gsc = GameStateController.Singleton;

			if ( gsc != null )
			{
				startingZ = GameObject.LocalPosition.z;
			}
		}
		else
		{
			if ( Synchronized )
			{
				t = gsc.GetSynchronizedTime();
				if ( t < 0 )
				{
					processedTime = t;
				}
				else
				{
					processedTime = MathX.Lerp( processedTime, t, Time.Delta * lerpRate );
				}

				currentLocalPosition = GameObject.LocalPosition;
				currentLocalPosition = new Vector3( currentLocalPosition.x, currentLocalPosition.y, startingZ + (MathF.Sin( processedTime / Period ) * Amplitude) );
				GameObject.LocalPosition = currentLocalPosition;
			}
			else
			{
				sinProgress += (Time.Delta / Period);

				currentLocalPosition = GameObject.LocalPosition;
				currentLocalPosition = new Vector3( currentLocalPosition.x, currentLocalPosition.y, startingZ + (MathF.Sin( sinProgress ) * Amplitude) );
				GameObject.LocalPosition = currentLocalPosition;
			}
		}*/
	}
}

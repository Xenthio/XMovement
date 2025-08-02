using Sandbox;
using System;

public sealed class SimpleRotate : Component
{
	[Property] public float Yaw { get; set; }
	[Property] public bool Synchronized { get; set; } // if true, derives rotation from Synced Roundtime

	const float lerpRate = 15;

	Angles currentAngles;
	//GameStateController gsc;
	float processedTime = 0;

	float startingYaw = 0f;
	float t;
	protected override void OnUpdate()
	{
		currentAngles = GameObject.LocalRotation.Angles();
		currentAngles.yaw += Yaw * Time.Delta;
		GameObject.LocalRotation = currentAngles;
		/*if (gsc == null)
		{
			gsc = GameStateController.Singleton;

			if (gsc != null)
			{
				startingYaw = GameObject.LocalRotation.Angles().yaw;
			}
		} else
		{
			if ( Synchronized )
			{
				t = gsc.GetSynchronizedTime();
				if (t < 0)
				{
					processedTime = t;
				} else
				{
					processedTime = MathX.Lerp( processedTime, t, Time.Delta * lerpRate );
				}

				currentAngles = GameObject.LocalRotation.Angles();
				currentAngles.yaw = startingYaw + (processedTime * Yaw);
				GameObject.LocalRotation = currentAngles;
			} else
			{
				currentAngles = GameObject.LocalRotation.Angles();
				currentAngles.yaw += Yaw * Time.Delta;
				GameObject.LocalRotation = currentAngles;
			}
		}*/
	}
}

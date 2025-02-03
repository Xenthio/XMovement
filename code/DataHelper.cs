#nullable enable

using System;
/// <summary>
/// Helpers for reading from / writing to <see cref="FileSystem.Data"/>.
/// </summary>
public static class DataHelper
{
	public static T? ReadJson<T>( string fileName )
	{
		if ( !FileSystem.OrganizationData.FileExists( fileName ) )
		{
			return default;
		}

		try
		{
			var text = FileSystem.OrganizationData.ReadAllText( fileName );
			T des = Json.Deserialize<T>( text.Base64Decode() );

			if (des == null)
			{
				Log.Warning( "Unable to parse " + fileName );
			} else
			{
				Log.Info( "Successfully parsed " + fileName );
			}

			return des;
		}
		catch ( Exception ex )
		{
			Log.Warning( ex );
			return default;
		}
	}

	public static bool WriteJson<T>( string fileName, T value )
	{
		try
		{
			var json = Json.Serialize( value );
			FileSystem.OrganizationData.WriteAllText( fileName, json.Base64Encode() );
			return true;
		}
		catch ( Exception ex )
		{
			Log.Warning( ex );
			return false;
		}
	}
}

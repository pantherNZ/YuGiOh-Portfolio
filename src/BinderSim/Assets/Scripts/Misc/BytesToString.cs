using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Compression;

namespace Compression
{
    public static class GZipProcessor
    {
        public static byte[] Compress( byte[] data )
        {
            using var compressedStream = new MemoryStream();
            using var zipStream = new GZipStream( compressedStream, CompressionMode.Compress );
            zipStream.Write( data, 0, data.Length );
            zipStream.Close();
            return compressedStream.ToArray();
        }

        public static byte[] Decompress( byte[] data )
        {
            using var compressedStream = new MemoryStream( data );
            using var zipStream = new GZipStream( compressedStream, CompressionMode.Decompress );
            using var resultStream = new MemoryStream();
            zipStream.CopyTo( resultStream );
            return resultStream.ToArray();
        }
    }

    public static class Deflate
    {
        public static byte[] Compress( byte[] data )
        {
            using var compressedStream = new MemoryStream();
            using var deflateStream = new DeflateStream( compressedStream, CompressionMode.Compress );
            deflateStream.Write( data, 0, data.Length );
            deflateStream.Close();
            return compressedStream.ToArray();
        }

        public static byte[] Decompress( byte[] data )
        {
            using var compressedStream = new MemoryStream( data );
            using var deflateStream = new DeflateStream( compressedStream, CompressionMode.Decompress );
            using var resultStream = new MemoryStream();
            deflateStream.CopyTo( resultStream );
            return resultStream.ToArray();
        }
    }
}

public static partial class StringHelper
{
    private const int maxChars = 91;
    private const int asciiCharStart = 33;

    public static string GetStringFromBytes( Byte[] bts )
    {
        StringBuilder str = new StringBuilder();
        foreach( var bt in bts )
        {
            if( bt <= maxChars )
            {
                str.Append( ( char )( bt + asciiCharStart ) );
            }
            else if( bt <= maxChars * 2 )
            {
                str.Append( 'A' );
                str.Append( ( char )( bt - maxChars + asciiCharStart ) );
            }
            else
            {
                str.Append( 'B' );
                str.Append( ( char )( bt - maxChars * 2 + asciiCharStart ) );
            }
            str[str.Length - 1] = FixChar( str[str.Length - 1], false );
        }
        return str.ToString();
    }

    public static Byte[] GetBytesFromString( string str )
    {
        List<Byte> bts = new List<byte>();
        for( int idx = 0; idx < str.Length; ++idx )
        {
            var c = str[idx];
            if( c == 'A' )
            {
                c = str[++idx];
                bts.Add( ( byte )( FixChar( c, true ) + maxChars - asciiCharStart ) );
            }
            else if( c == 'B' )
            {
                c = str[++idx];
                bts.Add( ( byte )( FixChar( c, true ) + maxChars * 2 - asciiCharStart ) );
            }
            else
            {
                bts.Add( ( byte )( FixChar( c, true ) - asciiCharStart ) );
            }
        }
        return bts.ToArray();
    }

    private static char FixChar( char c, bool import )
    {
        if( import )
        {
            if( c == '|' )
                return 'A';
            else if( c == '}' )
                return 'B';
        }
        else
        {
            if( c == 'A' )
                return '|';
            else if( c == 'B' )
                return '}';
        }
        return c;
    }
}
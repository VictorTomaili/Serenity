﻿using Microsoft.AspNetCore.WebUtilities;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Serenity.Web;

/// <summary>
/// Default implementation for <see cref="IScriptContent"/>
/// </summary>
public class ScriptContent : IScriptContent
{
    private readonly bool canCompress;
    private string hash;
    private readonly byte[] content;
    private byte[] gzipContent;
    private byte[] brotliContent;

    /// <summary>
    /// Creates a new instance of the class
    /// </summary>
    /// <param name="content">Content</param>
    /// <param name="time">Time</param>
    /// <param name="canCompress">Allow compression</param>
    /// <exception cref="ArgumentNullException"></exception>
    public ScriptContent(byte[] content, DateTime time, bool canCompress)
    {
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.canCompress = canCompress;
        Time = time;
    }

    /// <summary>
    /// Gets script generated time
    /// </summary>
    public DateTime Time { get; private set; }

    /// <summary>
    /// Gets script hash
    /// </summary>
    public string Hash 
    { 
        get
        {
            if (hash == null)
            {
                var md5 = MD5.Create();
                byte[] result = md5.ComputeHash(content);
                hash = WebEncoders.Base64UrlEncode(result);
            }

            return hash;
        }
    }

    /// <inheritdoc/>
    public byte[] Content => content;

    /// <inheritdoc/>
    public bool CanCompress => canCompress;

    /// <inheritdoc/>
    public byte[] CompressedContent
    {
        get
        {
            if (!canCompress)
                throw new InvalidOperationException("Script does not allow compression!");

            if (gzipContent == null)
            {
                using var cs = new MemoryStream(content.Length);
                using (var gz = new GZipStream(cs, CompressionMode.Compress))
                {
                    gz.Write(content, 0, content.Length);
                    gz.Flush();
                }

                gzipContent = cs.ToArray();
            }

            return gzipContent;
        }
    }

    /// <inheritdoc/>
    public byte[] BrotliContent
    {
        get
        {
            if (!canCompress)
                throw new InvalidOperationException("Script does not allow compression!");

            if (brotliContent == null)
            {
                using var cs = new MemoryStream(content.Length);
                using (var br = new BrotliStream(cs, CompressionMode.Compress))
                {
                    br.Write(content, 0, content.Length);
                    br.Flush();
                }

                brotliContent = cs.ToArray();
            }

            return brotliContent;
        }
    }
}
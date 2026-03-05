using File_manager.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace File_manager.Interfaces
{
    public interface IAsset
    {
        Guid Id { get; set; }

        string FullPath { get; set; }

        FileStatus Status { get; set; }
        AssetMetadata Baseline { get; set; }
    }
}
    
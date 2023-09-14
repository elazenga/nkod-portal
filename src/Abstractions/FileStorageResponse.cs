﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NkodSk.Abstractions
{
    public record FileStorageResponse(List<FileState> Files, int TotalCount, List<Facet> Facets)
    {

    }
}

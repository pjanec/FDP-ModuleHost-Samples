# Fdp.Modules.Geographic

## Overview

`Fdp.Modules.Geographic` is a **geospatial extensions plugin** for ModuleHost.Core. It provides coordinate system transformations, geographic projections, and spatial utilities for simulations that require real-world coordinate mapping.

This is a **Plugin Layer** component - it extends the generic kernel with geographic-specific functionality.

## Features

- **Coordinate Transformations**: Convert between different coordinate systems (WGS84, ECEF, ENU, etc.)
- **Geographic Components**: Components for storing geographic positions
- **Transform Services**: `IGeographicTransform` interface for coordinate conversions
- **Module Integration**: `GeographicModule` for managing geographic state

## Architecture

### Components

(Defined in `Components/` directory)
- Geographic position components
- Coordinate system metadata

### Systems

(Defined in `Systems/` directory)
- Coordinate conversion systems
- Spatial indexing systems

### Transforms

(Defined in `Transforms/` directory)
- `IGeographicTransform` - Abstraction for coordinate transforms
- Specific transform implementations (WGS84, ECEF, ENU, etc.)

### Module

- **GeographicModule**: Orchestrates geographic systems and provides transform services

## Usage

### Basic Setup

```csharp
using ModuleHost.Core;
using Fdp.Modules.Geographic;

// Setup Core
var world = new EntityRepository();
var kernel = new ModuleHostKernel(world, new EventAccumulator());

// Setup Geographic
var geoModule = new GeographicModule();
kernel.RegisterModule(geoModule);

// Use transforms in your systems
var wgs84ToEcef = geoModule.GetTransform("WGS84", "ECEF");
```

### Integration with Simulations

Geographic module is commonly used in:
- Vehicle simulations (see `Fdp.Examples.CarKinem`)
- Sensor simulations requiring real-world coordinates
- Multi-fidelity simulations with geographic context

## Dependencies

- **ModuleHost.Core** - Generic ECS kernel
- **Fdp.Kernel** - FDP ECS types

## Design Notes

- This plugin is independent of networking
- Applications define how to use geographic components alongside other components
- Transforms are stateless and can be used from any system
- Supports both 2D and 3D coordinate systems

## See Also

- [ModuleHost.Core](../ModuleHost/ModuleHost.Core/README.md) - Generic kernel
- [ARCHITECTURE-NOTES.md](../docs/ARCHITECTURE-NOTES.md) - Overall architecture
- [Fdp.Examples.CarKinem](../Fdp.Examples.CarKinem/) - Example usage

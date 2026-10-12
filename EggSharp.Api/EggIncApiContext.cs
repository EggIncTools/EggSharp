using EggSharp.Core;

namespace EggSharp.Api;

internal sealed record EggIncApiContext(ApiPreset Preset, IEiCodec Codec, AuxbrainSigner Signer, VersionTripleSource Versions, AuxbrainHosts Hosts);

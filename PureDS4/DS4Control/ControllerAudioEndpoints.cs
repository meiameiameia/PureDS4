using NAudio.CoreAudioApi;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DS4Windows
{
    internal enum ControllerAudioEndpointKind
    {
        Any,
        DualShock4,
        DualSense,
    }

    internal enum DirectSpeakerRouteDecision
    {
        Loopback,
        Direct,
        Pending,
    }

    internal enum DirectSpeakerEndpointOwnership
    {
        Unresolved,
        Owned,
        Unowned,
        Missing,
    }

    /// <summary>
    /// Endpoint discovery and direct-speaker routing shared by the controller
    /// audio paths. These helpers classify Windows audio endpoints and decide
    /// whether a request should be served by loopback capture or by writing
    /// directly to the controller.
    /// </summary>
    internal static class ControllerAudioEndpoints
    {
        public const string AutoDetectGameAudioEndpointId = "DS4Windows:AutoDetectDualSenseGameAudio";
        public const string DefaultSystemAudioEndpointId = "DS4Windows:DefaultSystemAudio";

        private const string EndpointHistoryValueName = "{4b416b7d-8501-40c1-acfd-97aa9bdc17c8},1";
        private const string RenderEndpointRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\";

        private static MMDevice FindCaptureEndpoint(string endpointId, string speakerEndpointId,
            ControllerAudioEndpointKind endpointKind)
        {
            bool useSystemDefault = string.Equals(endpointId,
                DefaultSystemAudioEndpointId, StringComparison.Ordinal) ||
                (string.IsNullOrEmpty(endpointId) &&
                endpointKind == ControllerAudioEndpointKind.Any);
            if (useSystemDefault)
            {
                return null;
            }

            try
            {
                using var enumerator = new MMDeviceEnumerator();
                bool autoDetect = string.IsNullOrEmpty(endpointId) ||
                    string.Equals(endpointId, AutoDetectGameAudioEndpointId,
                        StringComparison.Ordinal);
                MMDevice endpoint = autoDetect ?
                    FindActiveGameAudioEndpoint(enumerator, null, endpointKind) :
                    enumerator.GetDevice(endpointId);
                if (endpoint?.State != DeviceState.Active)
                {
                    return null;
                }

                if (string.Equals(endpoint.ID, speakerEndpointId, StringComparison.Ordinal))
                {
                    AppLogger.LogToGui("DualSense audio passthrough capture source cannot be the same as the speaker endpoint. Falling back to default audio endpoint.", true);
                    return null;
                }

                return endpoint;
            }
            catch
            {
                AppLogger.LogToGui("Controller audio passthrough capture source was not found. Falling back to default audio endpoint.", true);
                return null;
            }
        }

        private static bool DefaultEndpointMatches(string endpointId)
        {
            if (string.IsNullOrEmpty(endpointId))
            {
                return false;
            }

            try
            {
                using var enumerator = new MMDeviceEnumerator();
                MMDevice endpoint = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                return string.Equals(endpoint?.ID, endpointId, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsDualSenseEndpoint(MMDevice device)
        {
            ControllerAudioEndpointKind kind = ClassifyEndpoint(device);
            if (kind == ControllerAudioEndpointKind.DualSense)
            {
                return true;
            }

            // Older endpoint property stores can omit their USB instance ID.
            // Keep the historic generic fallback, but never mistake a positively
            // identified DS4 endpoint for a physical DualSense speaker.
            return kind == ControllerAudioEndpointKind.Any &&
                GetEndpointIdentity(device).IndexOf("Wireless Controller",
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsControllerAudioEndpoint(MMDevice device)
        {
            string identity = GetEndpointQuickIdentity(device);
            if (LooksLikeControllerAudioIdentity(identity))
            {
                return true;
            }

            // Avoid opening slow driver property stores for ordinary desktop
            // speakers, HDMI outputs, and virtual mixers. Only ambiguous devices
            // whose visible identity suggests a controller need the full probe.
            if (!LooksLikeAmbiguousControllerIdentity(identity))
            {
                return false;
            }

            identity = GetEndpointIdentity(device);
            return LooksLikeControllerAudioIdentity(identity);
        }

        private static string GetEndpointQuickIdentity(MMDevice endpoint)
        {
            if (endpoint == null)
            {
                return string.Empty;
            }

            try
            {
                return string.Join(" ", endpoint.ID ?? string.Empty,
                    endpoint.FriendlyName ?? string.Empty);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool LooksLikeControllerAudioIdentity(string identity)
        {
            return ClassifyEndpointIdentity(identity) != ControllerAudioEndpointKind.Any ||
                identity.IndexOf("Wireless Controller", StringComparison.OrdinalIgnoreCase) >= 0 ||
                identity.IndexOf("VIIPER", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool LooksLikeAmbiguousControllerIdentity(string identity)
        {
            return identity.IndexOf("controller", StringComparison.OrdinalIgnoreCase) >= 0 ||
                identity.IndexOf("sony", StringComparison.OrdinalIgnoreCase) >= 0 ||
                identity.IndexOf("playstation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                identity.IndexOf("054c", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static MMDevice FindActiveGameAudioEndpoint(MMDeviceEnumerator enumerator,
            string previousEndpointId = null,
            ControllerAudioEndpointKind preferredKind = ControllerAudioEndpointKind.Any,
            int preferredUsbipPort = -1)
        {
            IEnumerable<MMDevice> endpoints = enumerator
                .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                .Where(IsControllerAudioEndpoint);

            if (preferredKind != ControllerAudioEndpointKind.Any)
            {
                endpoints = endpoints.Where(endpoint =>
                {
                    ControllerAudioEndpointKind kind = ClassifyEndpoint(endpoint);
                    return kind == preferredKind || kind == ControllerAudioEndpointKind.Any;
                });
            }

            return endpoints
                .OrderByDescending(endpoint =>
                    EndpointMatchesUsbipPort(endpoint, preferredUsbipPort))
                .ThenByDescending(endpoint => EndpointScore(endpoint,
                    preferredKind, previousEndpointId))
                .FirstOrDefault();
        }

        private static bool EndpointMatchesUsbipPort(MMDevice endpoint,
            int preferredUsbipPort)
        {
            if (preferredUsbipPort < 0 || endpoint == null)
            {
                return false;
            }

            return TryGetEndpointUsbipPort(endpoint, out int endpointPort) &&
                endpointPort == preferredUsbipPort;
        }

        internal static bool TryGetEndpointUsbipPort(MMDevice endpoint,
            out int endpointPort)
        {
            endpointPort = -1;
            if (endpoint == null)
            {
                return false;
            }

            string interfacePath = GetEndpointProperty(endpoint,
                PropertyKeys.PKEY_Device_InterfaceKey);
            int pathStart = interfacePath.IndexOf(@"\\?\",
                StringComparison.Ordinal);
            if (pathStart > 0)
            {
                interfacePath = interfacePath.Substring(pathStart);
            }

            return !string.IsNullOrEmpty(interfacePath) &&
                Global.TryResolveUsbIpWin2Device(interfacePath,
                    out bool usbIpAncestor, out endpointPort) &&
                usbIpAncestor && endpointPort >= 0;
        }

        internal static ControllerAudioEndpointKind GetEndpointKind(OutContType outputType)
        {
            outputType = outputType.Normalize();
            return outputType switch
            {
                OutContType.ViiperDS4 => ControllerAudioEndpointKind.DualShock4,
                OutContType.ViiperDualSense or OutContType.ViiperDualSenseEdge =>
                    ControllerAudioEndpointKind.DualSense,
                _ => ControllerAudioEndpointKind.Any,
            };
        }

        internal static bool IsDirectSpeakerRequest(string endpointId,
            bool explicitEndpointOwnedByDirectSource)
        {
            endpointId ??= string.Empty;
            if (ProcessLoopbackWaveCapture.IsProcessEndpointId(endpointId))
            {
                return false;
            }
            if (string.IsNullOrEmpty(endpointId) ||
                string.Equals(endpointId, AutoDetectGameAudioEndpointId,
                    StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(endpointId, DefaultSystemAudioEndpointId,
                StringComparison.Ordinal))
            {
                return false;
            }

            return explicitEndpointOwnedByDirectSource;
        }

        internal static DirectSpeakerRouteDecision DecideDirectSpeakerRoute(
            string endpointId, bool directSourceCapable,
            bool directStreamActive,
            DirectSpeakerEndpointOwnership explicitEndpointOwnership)
        {
            endpointId ??= string.Empty;
            if (ProcessLoopbackWaveCapture.IsProcessEndpointId(endpointId))
            {
                return DirectSpeakerRouteDecision.Loopback;
            }
            if (string.Equals(endpointId, DefaultSystemAudioEndpointId,
                StringComparison.Ordinal))
            {
                return DirectSpeakerRouteDecision.Loopback;
            }

            if (!directSourceCapable)
            {
                return DirectSpeakerRouteDecision.Loopback;
            }

            bool automatic = string.IsNullOrEmpty(endpointId) ||
                string.Equals(endpointId, AutoDetectGameAudioEndpointId,
                    StringComparison.Ordinal);
            if (automatic)
            {
                return directStreamActive ? DirectSpeakerRouteDecision.Direct :
                    DirectSpeakerRouteDecision.Pending;
            }

            return explicitEndpointOwnership switch
            {
                DirectSpeakerEndpointOwnership.Owned when directStreamActive =>
                    DirectSpeakerRouteDecision.Direct,
                DirectSpeakerEndpointOwnership.Owned =>
                    DirectSpeakerRouteDecision.Pending,
                // VIIPER controller audio endpoints are recreated with a new
                // MMDevice GUID. An old concrete controller GUID must not
                // leave the physical speaker waiting forever. Once the
                // controller-bound direct stream is live, it is the safe
                // replacement for a saved endpoint that no longer exists.
                DirectSpeakerEndpointOwnership.Missing when directStreamActive =>
                    DirectSpeakerRouteDecision.Direct,
                DirectSpeakerEndpointOwnership.Missing =>
                    DirectSpeakerRouteDecision.Pending,
                DirectSpeakerEndpointOwnership.Unowned =>
                    DirectSpeakerRouteDecision.Loopback,
                _ => DirectSpeakerRouteDecision.Pending,
            };
        }

        internal static DirectSpeakerRouteDecision EvaluateDirectSpeakerRoute(
            string endpointId,
            ControllerAudioEndpointKind endpointKind,
            ViiperOutDevice directSpeakerSource)
        {
            bool capable = directSpeakerSource?.CanProvideDirectSpeakerPcm == true;
            bool active = directSpeakerSource?.SupportsDirectSpeakerPcm == true;
            DirectSpeakerEndpointOwnership ownership =
                DirectSpeakerEndpointOwnership.Unresolved;
            bool explicitEndpoint = !string.IsNullOrEmpty(endpointId) &&
                !string.Equals(endpointId, AutoDetectGameAudioEndpointId,
                    StringComparison.Ordinal) &&
                !string.Equals(endpointId, DefaultSystemAudioEndpointId,
                    StringComparison.Ordinal);
            if (capable && explicitEndpoint)
            {
                ownership = ResolveExplicitEndpointOwnership(endpointId,
                    endpointKind, directSpeakerSource);
            }

            return DecideDirectSpeakerRoute(endpointId, capable, active,
                ownership);
        }

        private static DirectSpeakerEndpointOwnership
            ResolveExplicitEndpointOwnership(
            string endpointId, ControllerAudioEndpointKind endpointKind,
            ViiperOutDevice directSpeakerSource)
        {
            if (string.IsNullOrEmpty(endpointId) ||
                directSpeakerSource == null)
            {
                return DirectSpeakerEndpointOwnership.Unresolved;
            }

            try
            {
                using var enumerator = new MMDeviceEnumerator();
                List<MMDevice> activeEndpoints = enumerator
                    .EnumerateAudioEndPoints(DataFlow.Render,
                        DeviceState.Active).ToList();
                try
                {
                    MMDevice exactEndpoint = activeEndpoints.FirstOrDefault(
                        endpoint => string.Equals(endpoint.ID, endpointId,
                            StringComparison.Ordinal));
                    if (exactEndpoint != null)
                    {
                        if (IsControllerEndpointSelection(
                                ClassifyEndpoint(exactEndpoint), endpointKind))
                        {
                            return DirectSpeakerEndpointOwnership.Owned;
                        }

                        return ResolveEndpointOwnership(exactEndpoint,
                            endpointKind, directSpeakerSource);
                    }

                    DirectSpeakerEndpointOwnership replacementResult =
                        DirectSpeakerEndpointOwnership.Unresolved;
                    foreach (MMDevice candidate in activeEndpoints.Where(
                        endpoint => EndpointReplaces(endpoint, endpointId)))
                    {
                        if (IsControllerEndpointSelection(
                                ClassifyEndpoint(candidate), endpointKind))
                        {
                            return DirectSpeakerEndpointOwnership.Owned;
                        }

                        DirectSpeakerEndpointOwnership candidateResult =
                            ResolveEndpointOwnership(candidate, endpointKind,
                                directSpeakerSource);
                        if (candidateResult ==
                            DirectSpeakerEndpointOwnership.Owned)
                        {
                            return candidateResult;
                        }

                        if (candidateResult ==
                            DirectSpeakerEndpointOwnership.Unowned)
                        {
                            replacementResult = candidateResult;
                        }
                    }

                    if (replacementResult !=
                        DirectSpeakerEndpointOwnership.Unresolved)
                    {
                        return replacementResult;
                    }
                }
                finally
                {
                    foreach (MMDevice endpoint in activeEndpoints)
                    {
                        endpoint.Dispose();
                    }
                }

                bool savedEndpointMissing = false;
                try
                {
                    using MMDevice savedEndpoint =
                        enumerator.GetDevice(endpointId);
                    if (savedEndpoint != null &&
                        IsControllerEndpointSelection(
                            ClassifyEndpoint(savedEndpoint), endpointKind))
                    {
                        return DirectSpeakerEndpointOwnership.Owned;
                    }

                    if (savedEndpoint?.State == DeviceState.Active)
                    {
                        return ResolveEndpointOwnership(savedEndpoint,
                            endpointKind, directSpeakerSource);
                    }

                }
                catch
                {
                    savedEndpointMissing = true;
                }

                return savedEndpointMissing
                    ? DirectSpeakerEndpointOwnership.Missing
                    : DirectSpeakerEndpointOwnership.Unresolved;
            }
            catch
            {
                return DirectSpeakerEndpointOwnership.Unresolved;
            }
        }

        internal static bool IsControllerEndpointSelection(
            ControllerAudioEndpointKind savedEndpointKind,
            ControllerAudioEndpointKind currentOutputKind)
        {
            bool savedIsSpecificController =
                savedEndpointKind == ControllerAudioEndpointKind.DualShock4 ||
                savedEndpointKind == ControllerAudioEndpointKind.DualSense;
            bool currentIsSpecificController =
                currentOutputKind == ControllerAudioEndpointKind.DualShock4 ||
                currentOutputKind == ControllerAudioEndpointKind.DualSense;
            // Concrete Sony endpoint GUIDs are recreated when VIIPER restarts
            // or changes persona. Preserve the user's controller-audio intent
            // across both same-persona recreation and DS4/DualSense changes.
            // Non-controller endpoints keep literal loopback semantics.
            return savedIsSpecificController && currentIsSpecificController;
        }

        private static DirectSpeakerEndpointOwnership ResolveEndpointOwnership(
            MMDevice endpoint, ControllerAudioEndpointKind endpointKind,
            ViiperOutDevice directSpeakerSource)
        {
            bool identityMatches = EndpointKindMatches(endpoint, endpointKind);
            string interfacePath = GetEndpointProperty(endpoint,
                PropertyKeys.PKEY_Device_InterfaceKey);
            int pathStart = interfacePath.IndexOf(@"\\?\",
                StringComparison.Ordinal);
            if (pathStart > 0)
            {
                interfacePath = interfacePath.Substring(pathStart);
            }

            bool interfacePathAvailable =
                !string.IsNullOrEmpty(interfacePath);
            int endpointPort = -1;
            bool usbIpAncestor = false;
            bool usbIpQueryResolved = interfacePathAvailable &&
                Global.TryResolveUsbIpWin2Device(interfacePath,
                    out usbIpAncestor, out endpointPort);

            return ClassifyDirectSpeakerEndpointOwnership(
                endpoint?.State == DeviceState.Active, identityMatches,
                interfacePathAvailable, usbIpQueryResolved, usbIpAncestor,
                endpointPort, directSpeakerSource?.DirectSpeakerUsbipPort ?? -1);
        }

        internal static DirectSpeakerEndpointOwnership
            ClassifyDirectSpeakerEndpointOwnership(bool endpointActive,
            bool controllerIdentityMatches, bool interfacePathAvailable,
            bool usbIpQueryResolved, bool usbIpAncestor, int endpointPort,
            int sourcePort)
        {
            if (!endpointActive)
            {
                return DirectSpeakerEndpointOwnership.Unresolved;
            }

            if (!controllerIdentityMatches)
            {
                return DirectSpeakerEndpointOwnership.Unowned;
            }

            if (!interfacePathAvailable)
            {
                return DirectSpeakerEndpointOwnership.Unresolved;
            }

            if (!usbIpQueryResolved)
            {
                return DirectSpeakerEndpointOwnership.Unresolved;
            }

            if (!usbIpAncestor)
            {
                return DirectSpeakerEndpointOwnership.Unowned;
            }

            if (endpointPort < 0 || sourcePort < 0)
            {
                return DirectSpeakerEndpointOwnership.Unresolved;
            }

            return endpointPort == sourcePort ?
                DirectSpeakerEndpointOwnership.Owned :
                DirectSpeakerEndpointOwnership.Unowned;
        }

        private static bool EndpointKindMatches(MMDevice endpoint,
            ControllerAudioEndpointKind expectedKind)
        {
            if (!IsControllerAudioEndpoint(endpoint))
            {
                return false;
            }

            ControllerAudioEndpointKind actualKind = ClassifyEndpoint(endpoint);
            return expectedKind == ControllerAudioEndpointKind.Any ||
                actualKind == expectedKind ||
                actualKind == ControllerAudioEndpointKind.Any;
        }

        internal static ControllerAudioEndpointKind ClassifyEndpointIdentity(string identity)
        {
            identity ??= string.Empty;
            string normalized = identity.Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty);

            if (ContainsSonyUsbIdentity(normalized, "05C4") ||
                ContainsSonyUsbIdentity(normalized, "09CC") ||
                identity.IndexOf("DualShock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                identity.IndexOf("DS4", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ControllerAudioEndpointKind.DualShock4;
            }

            if (ContainsSonyUsbIdentity(normalized, "0CE6") ||
                ContainsSonyUsbIdentity(normalized, "0DF2") ||
                identity.IndexOf("DualSense", StringComparison.OrdinalIgnoreCase) >= 0 ||
                identity.IndexOf("PS5", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ControllerAudioEndpointKind.DualSense;
            }

            return ControllerAudioEndpointKind.Any;
        }

        private static ControllerAudioEndpointKind ClassifyEndpoint(MMDevice endpoint)
        {
            return ClassifyEndpointIdentity(GetEndpointIdentity(endpoint));
        }

        private static bool ContainsSonyUsbIdentity(string normalizedIdentity,
            string productId)
        {
            return normalizedIdentity.IndexOf("VID054C", StringComparison.OrdinalIgnoreCase) >= 0 &&
                normalizedIdentity.IndexOf("PID" + productId,
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetEndpointIdentity(MMDevice endpoint)
        {
            if (endpoint == null)
            {
                return string.Empty;
            }

            var values = new List<string>
            {
                endpoint.ID ?? string.Empty,
                endpoint.FriendlyName ?? string.Empty,
                endpoint.DeviceFriendlyName ?? string.Empty,
            };

            AddEndpointProperty(values, endpoint, PropertyKeys.PKEY_Device_InstanceId);
            AddEndpointProperty(values, endpoint, PropertyKeys.PKEY_Device_ControllerDeviceId);
            AddEndpointProperty(values, endpoint, PropertyKeys.PKEY_Device_InterfaceKey);
            return string.Join(" ", values);
        }

        private static string GetEndpointProperty(MMDevice endpoint,
            PropertyKey propertyKey)
        {
            try
            {
                return endpoint?.Properties[propertyKey]?.Value?.ToString() ??
                    string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void AddEndpointProperty(List<string> values, MMDevice endpoint,
            PropertyKey propertyKey)
        {
            try
            {
                object value = endpoint.Properties[propertyKey]?.Value;
                if (value != null)
                {
                    values.Add(value.ToString());
                }
            }
            catch
            {
                // Endpoint property availability differs by Windows audio driver.
            }
        }

        private static int EndpointScore(MMDevice endpoint,
            ControllerAudioEndpointKind preferredKind, string previousEndpointId)
        {
            int score = 0;
            ControllerAudioEndpointKind actualKind = ClassifyEndpoint(endpoint);
            if (preferredKind != ControllerAudioEndpointKind.Any && actualKind == preferredKind)
            {
                score += 100;
            }
            else if (actualKind != ControllerAudioEndpointKind.Any)
            {
                score += 20;
            }

            string identity = GetEndpointIdentity(endpoint);
            if (identity.IndexOf("VIIPER", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                score += 10;
            }

            if (!string.IsNullOrEmpty(previousEndpointId) &&
                (string.Equals(endpoint.ID, previousEndpointId, StringComparison.Ordinal) ||
                EndpointReplaces(endpoint, previousEndpointId)))
            {
                score += 1000;
            }

            return score;
        }

        private static bool EndpointReplaces(MMDevice endpoint, string previousEndpointId)
        {
            string endpointId = endpoint?.ID ?? string.Empty;
            int keyStart = endpointId.LastIndexOf(".{", StringComparison.Ordinal);
            if (keyStart < 0)
            {
                return false;
            }

            try
            {
                string endpointKeyName = endpointId.Substring(keyStart + 1);
                using RegistryKey properties = Registry.LocalMachine.OpenSubKey(
                    RenderEndpointRegistryPath + endpointKeyName + @"\Properties");
                object history = properties?.GetValue(EndpointHistoryValueName);
                if (history is string[] endpointIds)
                {
                    return endpointIds.Any(id => string.Equals(id, previousEndpointId,
                        StringComparison.OrdinalIgnoreCase));
                }

                return history is string id && string.Equals(id, previousEndpointId,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}

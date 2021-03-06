﻿using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Magnesium
{
	public class MgDriver : IDisposable
	{
		private IMgEntrypoint mEntrypoint;
		public MgDriver (IMgEntrypoint entrypoint)
		{
			mEntrypoint = entrypoint;
		}

		private IMgInstance mInstance;
		public IMgInstance Instance {
			get
			{
				return mInstance;
			}
		}

		public void Initialize(MgApplicationInfo appInfo)
		{
			Initialize (appInfo, null, null);
		}

        public void Initialize(MgApplicationInfo appInfo, MgEnableExtensionsOption options)
        {
            string[] extensions = null;
            if (options == MgEnableExtensionsOption.ALL)
            {                

                MgExtensionProperties[] extensionProperties;
                var err = mEntrypoint.EnumerateInstanceExtensionProperties(null, out extensionProperties);

                Debug.Assert(err == Result.SUCCESS, err + " != Result.SUCCESS");

                var enabledExtensions = new List<string>();
                if (extensionProperties != null)
                {
                    foreach (var ext in extensionProperties)
                    {
                        enabledExtensions.Add(ext.ExtensionName);
                    }
                }
                extensions = enabledExtensions.ToArray();
            }

            Initialize(appInfo, null, extensions);
        }

        public void Initialize (MgApplicationInfo appInfo, string[] enabledLayerNames, string[] enabledExtensionNames)
		{
			var instCreateInfo = new MgInstanceCreateInfo{
				ApplicationInfo = appInfo,
				EnabledLayerNames = enabledLayerNames,
				EnabledExtensionNames = enabledExtensionNames,
			};

			var errorCode = mEntrypoint.CreateInstance (instCreateInfo, null, out mInstance);
			Debug.Assert (errorCode == Result.SUCCESS, errorCode + " != Result.SUCCESS");
		}

		public IMgLogicalDevice CreateLogicalDevice(IMgSurfaceKHR presentationSurface, MgEnableExtensionsOption option)
		{
            string[] extensions = null;

            IMgPhysicalDevice[] physicalDevices;
            var errorCode = mInstance.EnumeratePhysicalDevices(out physicalDevices);
            Debug.Assert(errorCode == Result.SUCCESS, errorCode + " != Result.SUCCESS");
            IMgPhysicalDevice firstPhysicalDevice = physicalDevices[0];

            if (option == MgEnableExtensionsOption.ALL)
            {

                MgExtensionProperties[] extensionProperties;
                var err = firstPhysicalDevice.EnumerateDeviceExtensionProperties(null, out extensionProperties);

                Debug.Assert(err == Result.SUCCESS, err + " != Result.SUCCESS");

                var enabledExtensions = new List<string>();
                if (extensionProperties != null)
                {
                    foreach (var ext in extensionProperties)
                    {
                        enabledExtensions.Add(ext.ExtensionName);
                    }
                }
                extensions = enabledExtensions.ToArray();
            }            

			return CreateDevice (firstPhysicalDevice, presentationSurface, MgQueueAllocation.One, MgQueueFlagBits.GRAPHICS_BIT, extensions);
		}

		//public IMgLogicalDevice CreateLogicalDevice(MgEnableExtensionsOption option)
		//{
		//	return CreateDevice (0, null, MgQueueAllocation.One, MgQueueFlagBits.GRAPHICS_BIT);
		//}

		static uint FindAppropriateQueueFamily (MgQueueFamilyProperties[] queueProps, MgQueueFlagBits requestedQueueType)
		{
			for (uint i = 0; i < queueProps.Length; ++i)
			{
				// Find a queue that supports gfx
				if ((queueProps [i].QueueFlags & requestedQueueType) == requestedQueueType)
				{
					return i;
				}
			}

			throw new Exception("Could not find a queue");	
		}

		static uint FindAppropriateQueueFamilyForSurface (
			IMgPhysicalDevice gpu, 
			IMgSurfaceKHR presentationSurface, 
			MgQueueFamilyProperties[] queueProps,
			MgQueueFlagBits requestedQueueType)
		{
			bool[] supportsPresent = new bool[queueProps.Length];
			// Iterate over each queue to learn whether it supports presenting:
			for (UInt32 i = 0; i < queueProps.Length; ++i)
			{
				gpu.GetPhysicalDeviceSurfaceSupportKHR (i, presentationSurface, ref supportsPresent [i]);
			}
			// Search for a graphics and a present queue in the array of queue
			// families, try to find one that supports both
			UInt32 requestedQueueNodeIndex = UInt32.MaxValue;
			UInt32 presentQueueNodeIndex = UInt32.MaxValue;
			for (UInt32 i = 0; i < queueProps.Length; i++)
			{
				var queue = queueProps [i];
				if ((queue.QueueFlags & requestedQueueType) != 0)
				{
					if (requestedQueueNodeIndex == UInt32.MaxValue)
					{
						requestedQueueNodeIndex = i;
					}
					if (supportsPresent [i])
					{
						requestedQueueNodeIndex = i;
						presentQueueNodeIndex = i;
						break;
					}
				}
			}
			if (presentQueueNodeIndex == UInt32.MaxValue)
			{
				// If didn't find a queue that supports both graphics and present, then
				// find a separate present queue.
				for (UInt32 i = 0; i < queueProps.Length; ++i)
				{
					if (supportsPresent [i])
					{
						presentQueueNodeIndex = i;
						break;
					}
				}
			}

			if (requestedQueueNodeIndex == UInt32.MaxValue)
			{
				throw new Exception ("Could not find queue of requested queue type");
			}

			// Generate error if could not find both a graphics and a present queue
			if (presentQueueNodeIndex == UInt32.MaxValue)
			{
				throw new Exception ("Could not find a presentation queue");
			}

			// VERBATIM from cube.c
			// TODO: Add support for separate queues, including presentation,
			//       synchronization, and appropriate tracking for QueueSubmit.
			// NOTE: While it is possible for an application to use a separate graphics
			//       and a present queues, this demo program assumes it is only using
			//       one:
			if (requestedQueueNodeIndex != presentQueueNodeIndex)
			{
				throw new Exception ("Could not find an common queue");
			}
			return requestedQueueNodeIndex;
		}

		public IMgLogicalDevice CreateDevice(IMgPhysicalDevice gpu, IMgSurfaceKHR presentationSurface, MgQueueAllocation requestCount, MgQueueFlagBits requestedQueueType, string[] enabledExtensions)
		{
			// Find a queue that supports graphics operations
			MgQueueFamilyProperties[] queueProps;
			gpu.GetPhysicalDeviceQueueFamilyProperties (out queueProps);
			Debug.Assert (queueProps != null);
			Debug.Assert (queueProps.Length >= 1);

			uint queueFamilyIndex = 0;
			queueFamilyIndex = presentationSurface != null 
				? FindAppropriateQueueFamilyForSurface (gpu, presentationSurface, queueProps, requestedQueueType) 
				: FindAppropriateQueueFamily (queueProps, requestedQueueType);

			uint noOfQueues = (requestCount == MgQueueAllocation.One) ? 1 : queueProps [queueFamilyIndex].QueueCount;

			var queuePriorities = new float[noOfQueues];
			for (uint i = 0; i < noOfQueues; ++i)
			{
				queuePriorities [i] = 0f;
			}

			var queueCreateInfo = new MgDeviceQueueCreateInfo {
				QueueFamilyIndex = queueFamilyIndex,
				QueueCount = noOfQueues,
				QueuePriorities = queuePriorities,
			};

			return CreateDevice (gpu, queueCreateInfo, enabledExtensions);
		}

		public IMgLogicalDevice CreateDevice(IMgPhysicalDevice gpu, MgDeviceQueueCreateInfo queueCreateInfo, string[] enabledExtensions)
		{
			if (gpu == null)
			{
				throw new ArgumentNullException (nameof(gpu));
			}

			if (queueCreateInfo == null)
			{
				throw new ArgumentNullException (nameof(queueCreateInfo));
			}

			var deviceCreateInfo = new MgDeviceCreateInfo {
				EnabledExtensionNames = enabledExtensions,
				QueueCreateInfos = new[] {
					queueCreateInfo
				},
			};

			IMgDevice device;
			var errorCode = gpu.CreateDevice (deviceCreateInfo, null, out device);
			Debug.Assert (errorCode == Result.SUCCESS, errorCode + " != Result.SUCCESS");		

			// Get the graphics queue
			var availableQueues = new IMgQueueInfo[queueCreateInfo.QueueCount];
			for (uint i = 0; i < queueCreateInfo.QueueCount; ++i)
			{
				IMgQueue queue;
				device.GetDeviceQueue (queueCreateInfo.QueueFamilyIndex, i, out queue);
				availableQueues [i] = new MgQueueInfo (i, gpu, device, queueCreateInfo.QueueFamilyIndex, queue);
			}

			return new MgLogicalDevice (gpu, device, availableQueues);
		}

		~MgDriver ()
		{
			Dispose (false);	
		}

		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
		}

		private bool mIsDisposed = false;
		protected virtual void Dispose(bool disposing)
		{
			if (mIsDisposed)
				return;

			if (mInstance != null)
				mInstance.DestroyInstance (null);

			mIsDisposed = true;
		}

    }
}


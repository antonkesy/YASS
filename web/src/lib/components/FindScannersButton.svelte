<script lang="ts">
	import type { ScannerDeviceDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { getHub } from '$lib/hub/hub';
	import { hubError } from '$lib/hub/errors';
	import { t } from '$lib/i18n/index.svelte';
	import { Icon } from '$lib/icons';
	import { scannerPrefs } from '$lib/presets';
	import { remember } from '$lib/scanners';
	import { toast } from '$lib/toast';

	let { onfound }: { onfound: (devices: ScannerDeviceDto[]) => void } = $props();

	let looking = $state(false);

	// For a scanner plugged in after start: the server's own look is cached for half a minute.
	async function look() {
		if (looking) return;
		looking = true;
		try {
			const devices = await getHub().hub.refreshDevices();
			onfound(devices);
			scannerPrefs.set(remember($scannerPrefs, devices));
			toast.show(
				devices.length === 0
					? t('common.scannersFoundNone')
					: t('common.scannersFound', { n: devices.length })
			);
		} catch (e) {
			toast.show(hubError(e));
		} finally {
			looking = false;
		}
	}
</script>

<button
	type="button"
	class="flex h-11 flex-none items-center gap-1.5 rounded-full px-3.5 text-[13px] font-bold text-accent transition-colors hover:bg-accent-hover50 disabled:opacity-60"
	onclick={look}
	disabled={looking}
	aria-busy={looking}
	data-testid="find-scanners"
>
	<span class="flex {looking ? 'animate-spin' : ''}"><Icon name="rescan" size={16} /></span>
	{looking ? t('common.lookingForScanners') : t('common.lookForScanners')}
</button>

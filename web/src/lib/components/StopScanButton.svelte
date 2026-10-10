<script lang="ts">
	import { t } from '$lib/i18n/index.svelte';
	import { Icon } from '$lib/icons';

	let {
		onstop,
		stopping = false,
		testid = 'stop-scan-button'
	}: {
		onstop: () => void;
		stopping?: boolean;
		testid?: string;
	} = $props();
</script>

<button
	type="button"
	class="flex h-[50px] w-full items-center justify-center gap-2 rounded-[14px] bg-accent-tint text-[15px] font-extrabold text-accent-deep transition-colors hover:bg-accent-100 disabled:pointer-events-none disabled:opacity-60"
	onclick={onstop}
	disabled={stopping}
	data-testid={testid}
>
	<Icon name="close" />
	{stopping ? t('review.stoppingScan') : t('review.stopScan')}
</button>

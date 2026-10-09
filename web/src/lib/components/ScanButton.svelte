<script lang="ts">
	import { Icon } from '$lib/icons';
	import { t } from '$lib/i18n/index.svelte';

	let {
		disabled = false,
		busy = false,
		reason = '',
		onclick
	}: {
		disabled?: boolean;
		busy?: boolean;
		/** Why the button is greyed out, shown on hover. Nothing to say when it is not. */
		reason?: string;
		onclick: () => void;
	} = $props();
</script>

<!--
	The tooltip hangs on the wrapper, not the button: a disabled button takes no pointer
	events, so it could never show one itself.
-->
<span
	class="flex w-full max-w-[340px] justify-center"
	title={disabled && !busy ? reason || undefined : undefined}
	data-testid="scan-button-wrap"
>
	<button
		type="button"
		class="flex h-16 w-full items-center justify-center gap-3 rounded-[18px] bg-linear-135 from-accent-light to-accent-grad2 text-[20px] font-extrabold tracking-[-0.01em] text-white shadow-scan transition-[transform,box-shadow] duration-200 ease-spring hover:-translate-y-0.5 hover:shadow-scan-hover active:scale-[0.98] disabled:pointer-events-none disabled:opacity-40"
		{disabled}
		aria-busy={busy}
		{onclick}
		data-testid="scan-button"
	>
		<Icon name="scan" />
		{busy ? t('home.scanningEllipsis') : t('home.scan')}
	</button>
</span>

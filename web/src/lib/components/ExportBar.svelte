<script lang="ts">
	import type { SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
	import { activePageCount } from '$lib/format';

	let {
		session,
		progress = null,
		allowDownload = false,
		busy = false,
		savedPath = null,
		onsave,
		ondownload,
		ondiscard
	}: {
		session: SessionDto;
		progress?: number | null;
		allowDownload?: boolean;
		busy?: boolean;
		savedPath?: string | null;
		onsave: () => void;
		ondownload: () => void;
		ondiscard: () => void;
	} = $props();

	const active = $derived(activePageCount(session));
	const scanning = $derived(session.State === SessionState.Scanning);
	const exporting = $derived(session.State === SessionState.Exporting || busy);
	const canExport = $derived(
		!scanning &&
			!exporting &&
			active > 0 &&
			(session.State === SessionState.Review || session.State === SessionState.Done)
	);
	let confirmDiscard = $state(false);

	function discard() {
		if (!confirmDiscard) {
			confirmDiscard = true;
			return;
		}
		confirmDiscard = false;
		ondiscard();
	}
</script>

<div
	class="fixed inset-x-0 bottom-0 z-10 border-t border-stone-200 bg-white/95 backdrop-blur"
	data-testid="bottom-bar"
>
	{#if exporting}
		<div
			class="h-1 w-full bg-stone-200"
			role="progressbar"
			aria-valuemin="0"
			aria-valuemax="100"
			aria-valuenow={progress ?? 0}
			aria-label="Export progress"
		>
			<div class="h-1 bg-teal-600 transition-all" style:width="{progress ?? 5}%"></div>
		</div>
	{/if}
	<div
		class="mx-auto flex max-w-6xl flex-wrap items-center gap-2 px-3 py-2 pb-[max(0.5rem,env(safe-area-inset-bottom))]"
	>
		<span class="mr-auto text-sm text-stone-700" data-testid="page-count">
			{#if savedPath}
				<span class="text-emerald-700" data-testid="saved-message">Saved ✓</span>
			{:else}
				{active} of {session.Pages.length} page{session.Pages.length === 1 ? '' : 's'}
			{/if}
		</span>
		<button
			type="button"
			class="min-h-11 rounded-lg px-3 text-sm font-medium {confirmDiscard
				? 'bg-rose-600 text-white'
				: 'text-rose-700 hover:bg-rose-50'} disabled:opacity-40"
			onclick={discard}
			onblur={() => (confirmDiscard = false)}
			disabled={scanning || exporting}
			data-testid="discard-button"
		>
			{confirmDiscard ? 'Really discard?' : 'Discard'}
		</button>
		{#if allowDownload}
			<button
				type="button"
				class="min-h-11 rounded-lg border border-teal-700 px-3 text-sm font-medium text-teal-800 hover:bg-teal-50 disabled:opacity-40"
				onclick={ondownload}
				disabled={!canExport}
				data-testid="download-button"
			>
				Download
			</button>
		{/if}
		<button
			type="button"
			class="min-h-11 rounded-lg bg-teal-700 px-4 text-sm font-semibold text-white hover:bg-teal-600 disabled:opacity-40"
			onclick={onsave}
			disabled={!canExport}
			data-testid="save-button"
		>
			{exporting ? 'Saving…' : 'Save'}
		</button>
	</div>
</div>

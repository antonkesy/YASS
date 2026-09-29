<script lang="ts">
	import type { SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { imageUrl } from '$lib/hub/hub';
	import { activePageCount, formatTime, sessionTitle } from '$lib/format';
	import { Icon } from '$lib/icons';

	let {
		sessions,
		template = '',
		busy = false,
		onpick,
		oncancel
	}: {
		/** Candidates, already excluding the scan being merged into. */
		sessions: SessionDto[];
		/** The file name template, which is what an unnamed scan is called. */
		template?: string;
		busy?: boolean;
		onpick: (id: string) => void;
		oncancel: () => void;
	} = $props();
</script>

<div
	class="fixed inset-0 z-50 flex animate-fade-in items-center justify-center bg-[rgb(19_26_43_/_0.38)] p-4 backdrop-blur-[3px]"
>
	<button
		type="button"
		class="absolute inset-0 cursor-default"
		aria-label="Cancel"
		onclick={oncancel}
	></button>

	<div
		class="relative flex max-h-full w-[min(460px,100%)] animate-pop-in flex-col gap-[18px] overflow-y-auto rounded-[26px] bg-surface p-6 shadow-modal"
		role="dialog"
		aria-modal="true"
		aria-label="Add pages from a past scan"
		data-testid="merge-modal"
	>
		<div class="flex flex-col gap-1">
			<h2 class="text-[20px] font-extrabold">Add pages from a past scan</h2>
			<p class="text-sm text-ink-muted">
				Its pages are copied to the end of this scan as their own document. The other scan is left
				as it is.
			</p>
		</div>

		{#if sessions.length === 0}
			<p class="rounded-[14px] bg-app px-4 py-6 text-center text-sm text-ink-muted">
				No other scan has pages to add.
			</p>
		{:else}
			<div class="flex flex-col gap-0.5" class:is-muted={busy}>
				{#each sessions as session, i (session.Id)}
					{@const first = session.Pages.find((p) => !p.Deleted && p.ThumbnailReady)}
					<button
						type="button"
						class="flex animate-page-in items-center gap-2.5 rounded-[14px] px-2.5 py-2 text-left transition-colors hover:bg-accent-hover50"
						style="animation-delay: {Math.min(i, 10) * 40}ms"
						onclick={() => onpick(session.Id)}
					>
						<span
							class="flex h-[38px] w-[30px] flex-none items-center justify-center overflow-hidden rounded-[4px] bg-surface shadow-page"
						>
							{#if first}
								<img
									src={imageUrl(session.Id, first.Index, first.Hash, 'thumb')}
									alt=""
									loading="lazy"
									class="max-h-full max-w-full object-contain"
								/>
							{/if}
						</span>
						<span class="flex min-w-0 flex-1 flex-col">
							<span class="truncate text-sm font-bold">{sessionTitle(session, template)}</span>
							<span class="text-xs text-ink-muted">
								{formatTime(session.CreatedAt)} · {activePageCount(session)} pages
							</span>
						</span>
						<span
							class="flex h-8 w-8 flex-none items-center justify-center rounded-full bg-accent-50 text-accent"
						>
							<Icon name="plus" size={16} />
						</span>
					</button>
				{/each}
			</div>
		{/if}

		<button
			type="button"
			class="h-11 self-end rounded-[14px] bg-ctl px-4 text-sm font-bold text-ink-3 transition-colors hover:bg-ctl-hover"
			onclick={oncancel}
		>
			Cancel
		</button>
	</div>
</div>

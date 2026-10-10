<script lang="ts">
	import type { SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { imageUrl } from '$lib/hub/hub';
	import { activePageCount, formatTime, sessionTitle } from '$lib/format';
	import { Icon } from '$lib/icons';
	import { t } from '$lib/i18n/index.svelte';
	import Modal from './Modal.svelte';

	let {
		sessions,
		template = '',
		onpick,
		oncancel
	}: {
		/** Candidates, already excluding the scan being merged into. */
		sessions: SessionDto[];
		template?: string;
		onpick: (id: string) => void;
		oncancel: () => void;
	} = $props();
</script>

<Modal
	title={t('dialogs.mergeTitle')}
	hint={t('dialogs.mergeHint')}
	cancelLabel={t('dialogs.cancel')}
	{oncancel}
	class="w-[min(460px,100%)] gap-[18px]"
	testid="merge-modal"
>
	{#if sessions.length === 0}
		<p class="rounded-[14px] bg-app px-4 py-6 text-center text-sm text-ink-muted">
			{t('dialogs.mergeNone')}
		</p>
	{:else}
		<div class="flex flex-col gap-0.5">
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
							{formatTime(session.CreatedAt)} · {t('dialogs.pages', {
								n: activePageCount(session)
							})}
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
		{t('dialogs.cancel')}
	</button>
</Modal>

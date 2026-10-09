<script lang="ts">
	import { useLayer } from '$lib/keys.svelte';
	import { backdropOut, cardOut } from '$lib/motion';
	import { t } from '$lib/i18n/index.svelte';

	let { onclose }: { onclose: () => void } = $props();

	useLayer(() => onclose());

	/**
	 * What each key does, grouped by where it works. Kept in step with the handlers by hand.
	 * Derived, so a change of language relabels it while it is open.
	 */
	const groups: { title: string; keys: [string[], string][] }[] = $derived([
		{
			title: t('dialogs.groupEverywhere'),
			keys: [
				[[t('dialogs.keyEsc')], t('dialogs.keyClose')],
				[['?'], t('dialogs.keyHelp')]
			]
		},
		{
			title: t('dialogs.groupReview'),
			keys: [
				[['←', '→'], t('dialogs.keyPrevNext')],
				[[t('dialogs.keyEnter')], t('dialogs.keyOpen')],
				[['R'], t('dialogs.keyRotateRight')],
				[[t('dialogs.keyShift'), 'R'], t('dialogs.keyRotateLeft')],
				[[t('dialogs.keyDel')], t('dialogs.keyDelete')],
				[['A'], t('dialogs.allPages')],
				[[t('dialogs.keyCtrl'), 'S'], t('dialogs.keySave')],
				[[t('dialogs.keyAlt'), '←'], t('dialogs.keyMove')]
			]
		},
		{
			title: t('dialogs.groupFullScreen'),
			keys: [
				[['←', '→'], t('dialogs.keyPrevNext')],
				[['Z'], t('dialogs.keyZoom')]
			]
		},
		{
			title: t('dialogs.allPages'),
			keys: [[['+', '−'], t('dialogs.keySize')]]
		}
	]);
</script>

<div
	class="fixed inset-0 z-60 flex animate-fade-in items-center justify-center bg-[rgb(19_26_43_/_0.38)] p-4 backdrop-blur-[3px]"
	out:backdropOut|global
>
	<button
		type="button"
		class="absolute inset-0 cursor-default"
		aria-label={t('dialogs.closeShortcuts')}
		onclick={onclose}
	></button>
	<div
		class="relative flex max-h-full w-[min(560px,100%)] animate-card-in flex-col gap-4 overflow-y-auto rounded-[26px] bg-surface p-6 text-ink shadow-modal"
		out:cardOut|global
		role="dialog"
		aria-modal="true"
		aria-label={t('dialogs.shortcutsTitle')}
		data-testid="keyboard-help"
	>
		<h2 class="text-[20px] font-extrabold">{t('dialogs.shortcutsTitle')}</h2>
		<div class="grid gap-5 sm:grid-cols-2">
			{#each groups as group (group.title)}
				<section class="flex flex-col gap-2">
					<h3 class="text-[11px] font-bold tracking-[0.06em] text-ink-faint uppercase">
						{group.title}
					</h3>
					<dl class="flex flex-col gap-1.5">
						{#each group.keys as [keys, what] (what)}
							<div class="flex items-center justify-between gap-3">
								<dt class="text-[13px] text-ink-2">{what}</dt>
								<dd class="flex flex-none gap-1">
									{#each keys as key (key)}
										<kbd
											class="min-w-[26px] rounded-[7px] bg-ctl px-1.5 py-0.5 text-center font-mono text-[12px] font-bold text-ink shadow-[inset_0_-1.5px_0_rgb(19_26_43_/_0.12)]"
											>{key}</kbd
										>
									{/each}
								</dd>
							</div>
						{/each}
					</dl>
				</section>
			{/each}
		</div>
	</div>
</div>

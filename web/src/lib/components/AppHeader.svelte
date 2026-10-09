<script lang="ts">
	import { resolve } from '$app/paths';
	import { page as route } from '$app/state';
	import { getHub, sortedSessions } from '$lib/hub/hub';
	import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
	import StatusPill from './StatusPill.svelte';
	import { Icon } from '$lib/icons';
	import { cameFrom } from '$lib/navigation';
	import { escapeCloses } from '$lib/keys.svelte';
	import { ISSUE_TEMPLATES } from '$lib/links';
	import { i18n, LOCALE_NAMES, LOCALES, t } from '$lib/i18n/index.svelte';

	let menu = $state<'language' | 'issue' | null>(null);
	escapeCloses(
		() => menu !== null,
		() => (menu = null)
	);

	// Icons only, each named by its aria-label and shown as a tooltip on hover.
	const button =
		'flex h-10 w-10 flex-none items-center justify-center rounded-[12px] bg-surface text-ink shadow-chip transition-colors hover:bg-accent-wash';
	const popup =
		'absolute top-[calc(100%+8px)] right-0 z-31 flex min-w-[200px] animate-pop-in flex-col rounded-[14px] bg-surface p-1.5 text-ink shadow-popover';
	const item =
		'flex min-h-[44px] w-full items-center rounded-[10px] px-3 text-left text-[13px] font-bold transition-colors hover:bg-accent-hover50';

	const client = getHub();
	const scanner = client.scanner;
	const connection = client.connection;
	const sessions = client.sessions;
	const exportProgress = client.exportProgress;

	// Home wears the name in the middle; every other route wears a way back to it.
	const atHome = $derived(route.url.pathname === resolve('/'));
	// Review is where the scanner's state matters mid-task, so it sits in the middle there.
	const atReview = $derived(route.params.id !== undefined);

	const scanning = $derived(
		sortedSessions($sessions).find((s) => s.State === SessionState.Scanning) ?? null
	);
	const exporting = $derived(
		Object.entries($exportProgress).find(([id, percent]) => {
			const s = $sessions.get(id);
			return s?.State === SessionState.Exporting && percent < 100;
		})?.[1] ?? null
	);
</script>

<!--
	Three columns rather than a flex row with a spacer, so whatever sits in the middle is
	centred on the viewport and not on the space left between the two ends.
-->
<header
	class="sticky top-0 z-20 grid h-16 grid-cols-[1fr_auto_1fr] items-center gap-2.5 bg-header px-4 backdrop-blur-[14px] sm:px-7"
>
	<div class="flex min-w-0 items-center gap-2.5">
		{#if !atHome}
			<!-- Label and arrow are one link: the words are the obvious thing to click. -->
			<a
				href={resolve('/')}
				onclick={() => cameFrom.leaving(route.params.id ?? '')}
				aria-label={t('common.backHome')}
				title={t('common.backHome')}
				class="group flex items-center gap-2.5 rounded-[12px] text-[15px] font-bold text-ink-3 transition-colors hover:text-ink"
			>
				<span
					class="flex h-10 w-10 flex-none items-center justify-center rounded-[12px] bg-surface text-ink shadow-chip transition-colors group-hover:bg-accent-wash"
				>
					<Icon name="back" />
				</span>
			</a>
		{/if}
	</div>

	<!--
		On review, a wide screen puts the page between a narrow strip and a wider sidebar, so
		its centre sits left of the window's: the pill moves by half the difference to stay
		over the page rather than over the gap beside it.
	-->
	<div
		class="flex min-w-0 items-center justify-center {atReview
			? 'lg:-translate-x-[calc((var(--review-aside-w)-var(--review-strip-w))/2)]'
			: ''}"
		data-testid="header-centre"
	>
		{#if atHome}
			<!-- The name is the whole of the brand here. A phone has the buttons beside it to fit,
			     so there it goes by its initials. -->
			<span
				class="truncate text-[17px] font-extrabold tracking-[-0.01em] text-ink sm:text-[20px]"
				data-testid="app-name"
			>
				<span class="sm:hidden" aria-hidden="true">YASS</span>
				<span class="max-sm:sr-only">{t('common.appName')}</span>
			</span>
		{:else if atReview}
			<StatusPill
				status={$scanner}
				connection={$connection}
				pages={scanning?.Pages.length ?? null}
			/>
		{/if}
	</div>

	<!-- Language, documentation, issues: all the app's own chrome, kept to three icons. -->
	<div class="flex items-center justify-end gap-1.5">
		{#if menu}
			<button
				type="button"
				class="fixed inset-0 z-30 cursor-default"
				aria-label={menu === 'language'
					? t('common.closeLanguageMenu')
					: t('common.closeIssueMenu')}
				onclick={() => (menu = null)}
			></button>
		{/if}

		<div class="relative">
			<button
				type="button"
				class="{button} gap-0.5 {menu === 'language' ? 'bg-accent-wash text-accent' : ''}"
				aria-haspopup="menu"
				aria-expanded={menu === 'language'}
				aria-label={t('common.language')}
				title={t('common.language')}
				onclick={() => (menu = menu === 'language' ? null : 'language')}
				data-testid="header-language"
			>
				<Icon name="globe" />
			</button>
			{#if menu === 'language'}
				<div class={popup} role="menu" data-testid="language-menu">
					{#each LOCALES as locale (locale)}
						<button
							type="button"
							role="menuitemradio"
							aria-checked={i18n.locale === locale}
							lang={locale}
							class="{item} justify-between gap-3"
							onclick={() => {
								i18n.set(locale);
								menu = null;
							}}
							data-testid="language-{locale}"
						>
							{LOCALE_NAMES[locale]}
							{#if i18n.locale === locale}<Icon name="checkSmall" />{/if}
						</button>
					{/each}
				</div>
			{/if}
		</div>

		<a
			href={resolve('/docs')}
			class={button}
			aria-label={t('common.documentation')}
			title={t('common.documentation')}
			data-testid="header-docs"
		>
			<Icon name="book" />
		</a>

		<div class="relative">
			<button
				type="button"
				class="{button} {menu === 'issue' ? 'bg-accent-wash text-accent' : ''}"
				aria-haspopup="menu"
				aria-expanded={menu === 'issue'}
				aria-label={t('common.reportIssue')}
				title={t('common.reportIssue')}
				onclick={() => (menu = menu === 'issue' ? null : 'issue')}
				data-testid="header-issue"
			>
				<Icon name="bug" />
			</button>
			{#if menu === 'issue'}
				<div class={popup} role="menu" data-testid="issue-menu">
					{#each ISSUE_TEMPLATES as template (template.id)}
						<a
							href={template.href}
							target="_blank"
							rel="noreferrer noopener external"
							role="menuitem"
							class={item}
							onclick={() => (menu = null)}
							data-testid="issue-{template.id}"
						>
							{t(template.label)}
						</a>
					{/each}
				</div>
			{/if}
		</div>
	</div>

	<!--
		Sub-page scan progress is not something the scanner reports, so the rail runs
		indeterminate while pages are arriving and switches to real percentages once an
		export is under way, which the hub does report.
	-->
	{#if scanning || exporting !== null}
		<div class="pointer-events-none absolute inset-x-0 bottom-0 h-[3px] overflow-hidden">
			{#if exporting !== null}
				<div
					class="h-full rounded-r-[3px] bg-linear-to-r from-accent-sky to-accent transition-[width] duration-100 ease-linear"
					style="width: {exporting}%"
				></div>
			{:else}
				<div class="h-full w-1/4 animate-rail bg-linear-to-r from-accent-sky to-accent"></div>
			{/if}
		</div>
	{/if}
</header>

export const REPO = 'https://github.com/antonkesy/YASS';

/** File names must match .github/ISSUE_TEMPLATE. */
export const ISSUE_TEMPLATES = (
	[
		{ id: 'bug', label: 'common.issueBug', file: 'bug_report.yml' },
		{
			id: 'feature',
			label: 'common.issueFeature',
			file: 'feature_request.yml'
		},
		{
			id: 'scanner',
			label: 'common.issueScanner',
			file: 'scanner_support.yml'
		}
	] as const
).map((t) => ({ ...t, href: `${REPO}/issues/new?template=${t.file}` }));

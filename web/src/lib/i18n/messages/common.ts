import type { Area } from '../types';

export const common = {
	backHome: {
		en: 'Back to home',
		de: 'Zurück zur Startseite',
		fr: "Retour à l'accueil",
		es: 'Volver al inicio'
	},
	appName: {
		en: 'Yet Another Slop Scanner',
		de: 'Yet Another Slop Scanner',
		fr: 'Yet Another Slop Scanner',
		es: 'Yet Another Slop Scanner'
	},
	language: { en: 'Language', de: 'Sprache', fr: 'Langue', es: 'Idioma' },
	lookForScanners: {
		en: 'Look for scanners',
		de: 'Nach Scannern suchen',
		fr: 'Rechercher des scanners',
		es: 'Buscar escáneres'
	},
	lookingForScanners: {
		en: 'Looking…',
		de: 'Suche…',
		fr: 'Recherche…',
		es: 'Buscando…'
	},
	scannersFoundNone: {
		en: 'No scanner found',
		de: 'Kein Scanner gefunden',
		fr: 'Aucun scanner trouvé',
		es: 'No se encontró ningún escáner'
	},
	scannersFound: {
		en: { one: 'Found {n} scanner', other: 'Found {n} scanners' },
		de: { one: '{n} Scanner gefunden', other: '{n} Scanner gefunden' },
		fr: { one: '{n} scanner trouvé', other: '{n} scanners trouvés' },
		es: { one: '{n} escáner encontrado', other: '{n} escáneres encontrados' }
	},
	switchToDark: {
		en: 'Switch to dark',
		de: 'Dunkel darstellen',
		fr: 'Passer en sombre',
		es: 'Cambiar a oscuro'
	},
	switchToLight: {
		en: 'Switch to light',
		de: 'Hell darstellen',
		fr: 'Passer en clair',
		es: 'Cambiar a claro'
	},
	closeLanguageMenu: {
		en: 'Close language menu',
		de: 'Sprachmenü schließen',
		fr: 'Fermer le menu des langues',
		es: 'Cerrar el menú de idiomas'
	},
	documentation: {
		en: 'Documentation',
		de: 'Dokumentation',
		fr: 'Documentation',
		es: 'Documentación'
	},
	reportIssue: {
		en: 'Report an issue',
		de: 'Problem melden',
		fr: 'Signaler un problème',
		es: 'Informar de un problema'
	},
	more: { en: 'More', de: 'Mehr', fr: 'Plus', es: 'Más' },
	closeMoreMenu: {
		en: 'Close menu',
		de: 'Menü schließen',
		fr: 'Fermer le menu',
		es: 'Cerrar el menú'
	},
	closeIssueMenu: {
		en: 'Close issue menu',
		de: 'Menü schließen',
		fr: 'Fermer le menu',
		es: 'Cerrar el menú'
	},
	issueBug: {
		en: 'Report a bug',
		de: 'Fehler melden',
		fr: 'Signaler un bug',
		es: 'Informar de un error'
	},
	issueFeature: {
		en: 'Request a feature',
		de: 'Funktion vorschlagen',
		fr: 'Proposer une fonctionnalité',
		es: 'Proponer una función'
	},
	issueScanner: {
		en: 'Scanner support',
		de: 'Scanner-Unterstützung',
		fr: 'Prise en charge du scanner',
		es: 'Compatibilidad del escáner'
	},
	docsTitle: {
		en: 'Documentation',
		de: 'Dokumentation',
		fr: 'Documentation',
		es: 'Documentación'
	},
	docsQuote: {
		en: 'You thought this &#@! would be documented?!?',
		de: 'Du dachtest, dieser &#@! wäre dokumentiert?!?',
		fr: 'Tu pensais que ce &#@! serait documenté ?!?',
		es: '¿¡¿Creías que esta &#@! estaría documentada?!?'
	},
	docsCat: {
		en: 'A cat laughing and pointing at you',
		de: 'Eine Katze, die lacht und auf dich zeigt',
		fr: 'Un chat qui rit en te montrant du doigt',
		es: 'Un gato que se ríe y te señala'
	},
	docsYou: { en: 'you', de: 'du', fr: 'toi', es: 'tú' },
	presetDocument: { en: 'Document', de: 'Dokument', fr: 'Document', es: 'Documento' },
	presetDoubleSided: { en: 'Double-sided', de: 'Beidseitig', fr: 'Recto verso', es: 'Doble cara' },
	presetReceipt: { en: 'Receipt', de: 'Beleg', fr: 'Reçu', es: 'Recibo' },
	presetPhoto: { en: 'Photo', de: 'Foto', fr: 'Photo', es: 'Foto' }
} satisfies Area;

# Pierwsze uruchomienie

UsageDock zbiera w jednym oknie limity subskrypcji oraz koszty API wielu kont. Wymaga Windows 11 x64. Jest niezależnym projektem i nie jest oficjalną aplikacją Anthropic ani OpenAI.

1. Rozpakuj cały plik ZIP wydania i uruchom `UsageDock.exe`. Jeśli wydanie zawiera instalator `-setup.exe`, możesz zamiast tego zainstalować aplikację dla bieżącego użytkownika.
2. Wybierz dodanie połączenia. Nadaj nazwę, po której rozpoznasz konto lub zespół.
3. Wybierz dostawcę i typ połączenia. Wprowadź dane w maskowanym polu albo jawnie wskaż obsługiwany plik poświadczeń CLI. Aplikacja nie przeszukuje samodzielnie pozostałych kont.
4. Dla API użyj klucza administracyjnego organizacji. Opcjonalnie ogranicz widok do workspace'u Anthropic lub projektu OpenAI. Zwykły klucz do generowania odpowiedzi może nie mieć uprawnień do raportów.
5. Odśwież dane. Zaznacz ulubione połączenia, które chcesz widzieć w widżecie.

Język programu wybierzesz w Ustawieniach. Dostępne są polski, angielski, niemiecki, francuski i hiszpański. Opcja automatyczna korzysta z języka wyświetlania Windows, a dla nieobsługiwanych języków wybiera angielski. Zmiana działa bez restartu, również w widżecie. Dotychczasowe instalacje zachowują polski; nowe korzystają domyślnie z ustawienia automatycznego. Nazwy kont pozostają bez zmian. Kwoty budżetu wpisuj z separatorem dziesiętnym wybranego języka, bez separatorów tysięcy.

Limity Codex dotyczą pracy w Codex na koncie ChatGPT; nie oznaczają limitów wszystkich rozmów w ChatGPT. Koszty API dotyczą bieżącego miesiąca według UTC. Budżet wpisany w aplikacji jest lokalnym progiem ostrzegania, a nie blokadą wydatków u dostawcy.

Połączenia abonamentowe używają eksperymentalnych endpointów. Gdy token lub sesja wygaśnie, ponownie podaj aktualne poświadczenie. Aplikacja nie obiecuje automatycznego logowania ani odświeżania tokenów. Brak danych jest pokazywany jako niedostępność, nie jako zerowe zużycie.

Poświadczenia są szyfrowane przez Windows DPAPI dla bieżącego użytkownika. Usunięcie połączenia usuwa zapisany sekret. Przed odinstalowaniem usuń połączenia, jeśli chcesz wyczyścić ich dane; samo usunięcie programu zachowuje ustawienia. Nie wysyłaj plików poświadczeń ani zrzutów z prywatnymi danymi do publicznych zgłoszeń.

Wydania nie są obecnie podpisane certyfikatem wydawcy. Sprawdź sumę SHA-256 względem pliku z tego samego zaufanego wydania. Nie ma serwera UsageDock ani telemetrii: zapytania trafiają bezpośrednio do wybranego dostawcy.


W wersji 0.3.0 zakładka Konta pokazuje zwartą tabelę połączeń. Wyszukiwarka filtruje wiersze, przyciski w nagłówku dodają połączenie i odświeżają dane, a ustawienia otworzysz z paska zakładek. Widżet pokazuje ulubione połączenia na przewijanej liście; przełącznik „Zawsze na wierzchu” steruje jego pozycją nad innymi oknami.

Statystyki i Historia dotyczą odświeżeń z bieżącej sesji aplikacji. Nie stanowią trwałego archiwum rozliczeń. Tryb demonstracyjny używa syntetycznych kont i nie zmienia zapisanych połączeń.

Zakładka Statystyki pokazuje wszystkie dostępne okna limitów abonamentowych oraz osobne zestawienie kosztów API. Historia porządkuje zdarzenia bieżącej sesji; filtry pomagają znaleźć odczyty i problemy, a wyczyszczenie listy nie usuwa kont.

Ustawienia są podzielone na sekcje wyglądu, odświeżania, powiadomień, uruchamiania i widżetu. Motyw zmienisz jednym kliknięciem w nagłówku panelu lub widżetu, albo w sekcji Wygląd. Przełączenie jasnego i ciemnego motywu działa natychmiast i jest zapamiętywane. Pozostałe zmiany wymagają zapisu. Niezapisany formularz pozostaje zachowany podczas odświeżania i przechodzenia między zakładkami. Nieprawidłowy interwał odświeżania jest oznaczany bezpośrednio w formularzu.



W wersji 0.4.0 konta Codex pokazują również liczbę dostępnych zapasowych resetów oraz terminy ich ważności. Wybierz „Zarządzaj resetami”, sprawdź konto i konkretny reset, a następnie potwierdź jego użycie. Taki reset może odnowić wykorzystane okna pięciogodzinne i tygodniowe; nie jest zakupem dodatkowych kredytów. Jeśli odczyt jest niedostępny, aplikacja nie zastępuje go liczbą zero.

Terminy odnowienia limitów mają konkretną lokalną datę i godzinę oraz dokładniejsze odliczanie, np. „za 6 dni 3 godz.”. Pełny termin jest dostępny również w podpowiedzi w widżecie. Po upływie terminu potrzebny jest nowy odczyt od dostawcy, aby potwierdzić odnowienie limitu.

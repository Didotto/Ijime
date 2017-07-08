-- phpMyAdmin SQL Dump
-- version 4.6.5.2
-- https://www.phpmyadmin.net/
--
-- Host: 127.0.0.1
-- Creato il: Lug 05, 2017 alle 23:08
-- Versione del server: 10.1.21-MariaDB
-- Versione PHP: 5.6.30

SET SQL_MODE = "NO_AUTO_VALUE_ON_ZERO";
SET time_zone = "+00:00";


/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;

--
-- Database: `testijime`
--

-- --------------------------------------------------------

--
-- Struttura della tabella `groups`
--

CREATE TABLE `groups` (
  `id_group` int(11) NOT NULL,
  `name` char(20) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=latin1;

--
-- Dump dei dati per la tabella `groups`
--

INSERT INTO `groups` (`id_group`, `name`) VALUES
(1, 'utente'),
(2, 'psicologo'),
(3, 'admin');

-- --------------------------------------------------------

--
-- Struttura della tabella `messages`
--

CREATE TABLE `messages` (
  `id_message` bigint(20) UNSIGNED NOT NULL,
  `message` mediumtext NOT NULL,
  `sent` tinyint(1) NOT NULL DEFAULT '0',
  `dtime` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `fromU` int(10) UNSIGNED DEFAULT NULL,
  `toU` int(10) UNSIGNED DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=latin1;



--
-- Struttura stand-in per le viste `psicologi`
-- (Vedi sotto per la vista effettiva)
--
CREATE TABLE `psicologi` (
`id_user` int(10) unsigned
,`username` char(20)
);

-- --------------------------------------------------------

--
-- Struttura della tabella `users`
--

CREATE TABLE `users` (
  `id_user` int(10) UNSIGNED NOT NULL,
  `username` char(20) NOT NULL,
  `psw` varchar(40) NOT NULL,
  `connected` tinyint(1) DEFAULT '0',
  `mail` varchar(40) NOT NULL,
  `last_ip` int(10) UNSIGNED DEFAULT NULL,
  `last_country` char(3) DEFAULT NULL,
  `last_city` varchar(50) DEFAULT NULL,
  `cod_group` int(11) DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=latin1;



--
-- Struttura per la vista `psicologi`
--
DROP TABLE IF EXISTS `psicologi`;

CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`localhost` SQL SECURITY DEFINER VIEW `psicologi`  AS  select `users`.`id_user` AS `id_user`,`users`.`username` AS `username` from (`users` join `groups`) where ((`users`.`cod_group` = `groups`.`id_group`) and (`groups`.`name` = 'psicologo') and (`users`.`connected` = 1)) ;

--
-- Indici per le tabelle scaricate
--

--
-- Indici per le tabelle `groups`
--
ALTER TABLE `groups`
  ADD PRIMARY KEY (`id_group`);

--
-- Indici per le tabelle `messages`
--
ALTER TABLE `messages`
  ADD PRIMARY KEY (`id_message`),
  ADD KEY `fromU` (`fromU`),
  ADD KEY `toU` (`toU`);

--
-- Indici per le tabelle `users`
--
ALTER TABLE `users`
  ADD PRIMARY KEY (`id_user`),
  ADD UNIQUE KEY `username` (`username`,`mail`),
  ADD KEY `cod_group` (`cod_group`),
  ADD KEY `username_2` (`username`,`psw`);

--
-- AUTO_INCREMENT per le tabelle scaricate
--

--
-- AUTO_INCREMENT per la tabella `groups`
--
ALTER TABLE `groups`
  MODIFY `id_group` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=4;
--
-- AUTO_INCREMENT per la tabella `messages`
--
ALTER TABLE `messages`
  MODIFY `id_message` bigint(20) UNSIGNED NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=109;
--
-- AUTO_INCREMENT per la tabella `users`
--
ALTER TABLE `users`
  MODIFY `id_user` int(10) UNSIGNED NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=21;
--
-- Limiti per le tabelle scaricate
--

--
-- Limiti per la tabella `messages`
--
ALTER TABLE `messages`
  ADD CONSTRAINT `messages_ibfk_1` FOREIGN KEY (`fromU`) REFERENCES `users` (`id_user`) ON DELETE NO ACTION ON UPDATE CASCADE,
  ADD CONSTRAINT `messages_ibfk_2` FOREIGN KEY (`toU`) REFERENCES `users` (`id_user`) ON DELETE NO ACTION ON UPDATE CASCADE;

--
-- Limiti per la tabella `users`
--
ALTER TABLE `users`
  ADD CONSTRAINT `users_ibfk_1` FOREIGN KEY (`cod_group`) REFERENCES `groups` (`id_group`) ON DELETE NO ACTION ON UPDATE CASCADE;

/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*
/*
   Copyright 2017 Davide Balice

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
*/
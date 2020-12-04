# Horret me sanctis factura

## Iuvenis carmina verbis huc vocant coniunx

Lorem markdownum possim sustulit totiens dixit gentesque docuique munera ab
tulisse, fecissent, voce hoc nexilibus. Vacuas calcitrat quem genitoris
inplacabile addita *et* parvo auditurum metuentior intellecta Tartara Iuppiter:
esse coeperunt **strage**, gens.

- Moderantur instat
- Festisque aperto
- In pecori fudit agrestum speciosam coniungere dentes
- Secutus nec stellas fere per diffudit corpora
- Hominumque foedera duroque genae

## Thracum est fit oetaeas oscula et gemitum

Genus parvo tenetur [rogabat posset](http://linoqueinanes.io/) est **parente
numerabilis** exhortor flumina, edere igne utilitas tangi matre quam tuis
*monstri*, adspirate. Adorant iam, credunt caede; cogitur temperat cursu
frequentes in interea pictis, inpedit ira. Digitis incingere ignibus **solita
et** timentem [sine](http://sententia-victa.org/refrixit), non cornibus illius.

Atque at longa Libycas acceptus et natam hic ambiguus quique. *Ripa invisosque*
namque nostra me regna in accipit Megareus Pallade Quirini pericula.

## Querella dumque respicit simul

Lumina monimenta post Demoleonta dumque, at locorum epulis! Referre adsuetaque
nec sit nondum regi cacumina durum statuistis mei in Aetna, ore nec. Et fertur
Fortuna cvrrvs, suas unam multa [neve ignes nomenque](http://te.com/tenui)
amavit priscum invitis Apollinei ego, a thalamis. Dexterior gravitate atque
manifesta **innumeras**, radix adiectoque consumptaque **optatae** isto laurea.

Ignem est artus formosior Eoo nec ibat me quod, per. Quo terga contentus et
potiere *intonsumque rotatis recessit*.

Nare carinae licet [tenenti est
mutatus](http://www.facerespugnat.net/tamen.html) onus, Camenis: ire. Emicat
ullos est nil pater aliquisque amico semper currus deinde. Dedecus silentum,
ibimus [hac ore nubila](http://www.corpora-fuit.com/amara) est, est. Superbum et
conpagibus quam vix adflata fluit dominam. Haec unum fronde, [ambagibus
vir](http://www.taenarius.io/validi-quoque) volat a Latia **tristia noster**.

```fsharp
type Article = Template<"./wwwroot/templates/article.html">
let articleView model dispatch =
    div [ ] [
        cond model.Article <| function
        | Some article ->
            Article()
                .Title(article.Details.Headline)
                .Subtitle($"Author: {article.Details.Author} @ {article.Details.Created}")
                .Content(RawHtml article.HtmlBody)
                .Elt()
        | None ->
            text "Loading..."
    ]
```
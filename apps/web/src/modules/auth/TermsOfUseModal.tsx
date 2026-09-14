import { useEffect, useRef } from 'react';
import './terms-of-use.css';

interface TermsOfUseModalProps {
  open: boolean;
  onClose: () => void;
}

export function TermsOfUseModal({ open, onClose }: TermsOfUseModalProps) {
  const closeButtonRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!open) return;

    const previousOverflow = document.body.style.overflow;
    const previouslyFocused = document.activeElement instanceof HTMLElement
      ? document.activeElement
      : null;

    document.body.style.overflow = 'hidden';
    window.requestAnimationFrame(() => closeButtonRef.current?.focus());

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') onClose();
    }

    document.addEventListener('keydown', handleKeyDown);

    return () => {
      document.body.style.overflow = previousOverflow;
      document.removeEventListener('keydown', handleKeyDown);
      previouslyFocused?.focus();
    };
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div
      className="terms-modal"
      role="presentation"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) onClose();
      }}
    >
      <section
        className="terms-modal__dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="terms-modal-title"
      >
        <header className="terms-modal__header">
          <div>
            <span>Cadastro Cidademdia</span>
            <h2 id="terms-modal-title">Termos de Uso e Condições de Navegação</h2>
          </div>
          <button
            ref={closeButtonRef}
            className="terms-modal__close"
            type="button"
            aria-label="Fechar Termos de Uso"
            onClick={onClose}
          >
            <i className="fa-solid fa-xmark" aria-hidden="true" />
          </button>
        </header>

        <div className="terms-modal__content">
          <p>
            Bem-vindo ao CIDADEMDIA. Estes Termos de Uso regulam o acesso e a utilização da plataforma digital
            privada que visa facilitar a comunicação entre cidadãos e entidades da Administração Pública (direta,
            indireta ou concessionárias de serviços públicos).
          </p>
          <p>Ao se cadastrar e utilizar a plataforma, você concorda integralmente com as regras aqui descritas.</p>
          <p>
            Este Termo e Condições Gerais (“Termo”) aplica-se ao uso dos serviços oferecidos pela Econtatos
            Publicidade.Ltda.., sociedade devidamente inscrita no CNPJ/MF sob o nº 37.911.116/0001-70, gestora da
            plataforma composta do site www.cidademdia.com.br, e dos aplicativos nas versões Android e IOS e dos
            serviços objeto deste Termo, doravante denominada CIDADEMDIA, a PESSOA FÍSICA ou JURÍDICA maior e capaz,
            que tenha preenchido o Cadastro, cujos dados passam a ser parte integrante deste Termo, bem como que tenha
            “aceitado” eletronicamente todas as cláusulas do mesmo e todas as demais políticas disponíveis no site e
            aplicativo, doravante denominado USUÁRIO.
          </p>
          <p>
            Os serviços disponíveis pela econtatos no site www.cidademdia.com.br e aplicativo cidademdia para versões
            Android e IOS serão regidos pelas cláusulas e condições abaixo.
          </p>
          <p>
            Ao aceitar eletronicamente o presente Termo, através do clique no botão “Li e aceito os Termos de uso” da
            página de cadastro complementar a esta, o USUÁRIO estará automaticamente aderindo e concordando em se
            submeter integralmente a seus termos e condições e de qualquer de suas alterações futuras, além de aceitar
            as disposições das políticas do site.
          </p>

          <h3>Cláusula 1ª - Das definições</h3>
          <p>1.1 Para exata compreensão e interpretação dos direitos e obrigações previstos no presente Termo, são adotadas as seguintes definições:</p>
          <p><strong>a) CADASTRO:</strong> ficha cadastral completa com os dados pessoais do USUÁRIO, inclusive com email, o número do celular com DDD e SENHA do USUÁRIO, obrigatoriamente preenchida a fim de obter o LOGIN para a utilização da plataforma e dos Serviços da CIDADEMDIA.</p>
          <p><strong>b) LOGIN:</strong> trata-se do email e SENHA informados pelo USUÁRIO no ato do seu CADASTRO, que usará para acessar os Serviços CIDADEMDIA e o seu CADASTRO;</p>
          <p><strong>c) PLATAFORMA CIDADEMDIA:</strong> plataforma eletrônica (SITE e/ou aplicativo móvel) que tem como foco auxiliar seus usuários na publicação, distribuição e divulgação através de registro de ocorrências nas diversas áreas de atuação, e serviços públicos além de apresentar conteúdos complementares de ações publicitarias, seja através de campanhas de compartilhamento, seja através do inter-relacionamento com os demais USUÁRIOS e/ou os Clientes Master da plataforma.</p>
          <p><strong>d) FERRAMENTAS:</strong> os serviços à disposição dos USUÁRIOS oferecidos pela econtatos na PLATAFORMA econtatos;</p>
          <p><strong>e) SENHA:</strong> sequência de letras e números escolhida pelo USUÁRIO, composta de no mínimo 6 (seis) caracteres, a qual deverá ser previamente informada pelo USUÁRIO quando do acesso ao SITE CIDADEMDIA não terá acesso a esta senha, portanto se o USUÁRIO quiser recuperar seu acesso, a CIDADEMDIA encaminhará um email para o email cadastrado na plataforma econtatos para, a partir deste, gerar uma nova SENHA. Sendo assim, com este procedimento, a SENHA poderá ser alterada a qualquer momento pelo USUÁRIO;</p>
          <p><strong>f) SITE:</strong> portal eletrônico da CIDADEMDIA, localizado no endereço www.cidademdia.com.br, por meio do qual o USUÁRIO poderá solicitar a disponibilização dos Serviços econtatos, mediante preenchimento de CADASTRO, informação de LOGIN e SENHA de acesso próprios.</p>

          <h3>1. Objeto e Natureza do Serviço</h3>
          <ul>
            <li><strong>Canal Intermediário:</strong> O CIDADEMDIA é uma ferramenta tecnológica privada de controle social. Ela funciona exclusivamente como intermediária para o envio de relatos sobre serviços públicos.</li>
            <li><strong>Sem Vínculo Governamental:</strong> Esta plataforma não é um órgão oficial do Governo, Prefeitura ou Estado. Ela não possui poder coercitivo ou administrativo para obrigar o poder público a executar serviços ou responder às demandas.</li>
            <li><strong>Canais Oficiais:</strong> O uso desta plataforma não substitui as Ouvidorias Oficiais do Estado ou Município e não suspende prazos legais para recursos ou processos administrativos.</li>
          </ul>

          <h3>2. Cadastro e Responsabilidade do Usuário</h3>
          <ul>
            <li><strong>Dados Verdadeiros:</strong> O usuário compromete-se a fornecer dados cadastrais (Nome, CPF, E-mail) verídicos e atualizados. O uso de identidades falsas ou de terceiros resultará na exclusão imediata da conta.</li>
            <li><strong>Responsabilidade Civil e Penal:</strong> O usuário é o único e exclusivo responsável pelo teor de suas publicações, comentários e imagens anexadas, respondendo civil e criminalmente por qualquer falsidade ou dano causado.</li>
          </ul>

          <h3>3. Regras de Conduta e Moderação de Conteúdo</h3>
          <p>Para garantir um ambiente construtivo e focado na melhoria dos serviços, é expressamente proibido:</p>
          <ul>
            <li><strong>Ofensas Pessoais:</strong> Publicar injúrias, calúnias, difamações ou termos de baixo calão contra servidores públicos, políticos ou funcionários de concessionárias. As críticas devem focar estritamente no serviço prestado, não nas pessoas físicas.</li>
            <li><strong>Uso Político-Partidário:</strong> Utilizar o espaço para campanhas eleitorais, propagandas de candidatos, partidos ou ideologias políticas.</li>
            <li><strong>Violação de Privacidade:</strong> Anexar fotos ou vídeos que exponham rostos de terceiros, placas de veículos particulares ou dados pessoais sem consentimento prévio.</li>
            <li><strong>Fatos Fictícios:</strong> Criar reclamações falsas ou inflar artificialmente o número de queixas sobre um mesmo problema (duplicidade intencional).</li>
          </ul>
          <p>O [Nome da Plataforma] reserva-se o direito de moderar, editar ou remover conteúdos que violem estas regras, sem aviso prévio.</p>

          <h3>4. Privacidade e Compartilhamento de Dados (LGPD)</h3>
          <ul>
            <li><strong>Visibilidade Pública:</strong> O texto da reclamação, as fotos do local (rua, hospital, etc.) e o primeiro nome do usuário ficarão visíveis publicamente para fins de transparência e controle social.</li>
            <li><strong>Envio ao Órgão Público:</strong> Para que a reclamação seja analisada, o usuário autoriza expressamente o compartilhamento de seus dados de identificação (como CPF e e-mail) com o órgão público ou concessionária responsável pela demanda.</li>
            <li><strong>Segurança:</strong> Os dados pessoais serão tratados em estrita observância à Lei Geral de Proteção de Dados (LGPD) e não serão vendidos ou compartilhados com fins publicitários de terceiros.</li>
          </ul>

          <h3>5. Limitação de Responsabilidade da Plataforma</h3>
          <ul>
            <li><strong>Garantia de Solução:</strong> A plataforma não garante que o problema relatado será resolvido pelo órgão competente, visto que a execução do serviço depende do orçamento, planejamento e discricionariedade da Administração Pública.</li>
            <li><strong>Marco Civil da Internet:</strong> Nos termos do Art. 19 da Lei 12.965/14, a plataforma não responde civilmente por danos decorrentes de conteúdos gerados por seus usuários, salvo se descumprir ordem judicial específica de remoção.</li>
          </ul>

          <h3>6. Disposições Finais</h3>
          <ul>
            <li><strong>Alterações nos Termos:</strong> Estes termos poderão ser atualizados a qualquer momento. O uso contínuo da plataforma após as alterações constituirá aceitação das novas regras.</li>
          </ul>
          <p><strong>Foro:</strong> Fica eleito o Foro da Comarca de [Sào Paulo / SP] para dirimir quaisquer dúvidas ou litígios decorrentes deste documento.</p>
        </div>

        <footer className="terms-modal__footer">
          <button type="button" onClick={onClose}>Fechar</button>
        </footer>
      </section>
    </div>
  );
}
